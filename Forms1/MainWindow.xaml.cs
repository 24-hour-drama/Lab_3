using Library;
using Library.Lab2;
using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Library.Lab3;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;

namespace Forms1
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        NightlightState newNightlight;
        NightlightController controller = new NightlightController();
        CheckBox chk_power; //Создаём ссылку заранее
        Slider sld_brightness;
        ComboBox cmb_colors;
        Label lbl_actuall_brightness;
        List<string> colors = new List<string> { "Warm white", "Cold white", "White" };
        string pre_txt = "";
        string post_txt = "";
        // ------ Поля для вкладки «Циклы» ------
        private CycleAnalyzerService _cycleService = new CycleAnalyzerService();
        private ObservableCollection<ArrayItem> _arrayData = new ObservableCollection<ArrayItem>();
        private CycleMode _selectedMode = CycleMode.None;
        private bool _loopInitialized = false;
        private int[] _initSnapshot;
        private bool _isRunning = false;
        private CancellationTokenSource _runCts;

        private string currentHoareTriple = "";
        public MainWindow()
        {
            InitializeComponent();

            OperationsListBox.Items.Add("Операция 1: Включение");
            OperationsListBox.Items.Add("Операция 2: Настройка яркости");
            OperationsListBox.Items.Add("Операция 3: Настройка цвета");
            OperationsListBox.Items.Add("Операция 4: Таймер");
            OperationsListBox.Items.Add("Операция 5: Выключение");

            newNightlight = new NightlightState(); //Создаём новый объект для новой формы

            if (cmbPresets != null)
            {
                cmbPresets.Items.Add(new ComboBoxItem { Content = "1. Max из двух", Tag = "Max" });
                cmbPresets.Items.Add(new ComboBoxItem { Content = "2. Последовательность присваиваний", Tag = "Sequence" });
                cmbPresets.Items.Add(new ComboBoxItem { Content = "3. Квадратное уравнение", Tag = "Quadratic" });
                cmbPresets.Items.Add(new ComboBoxItem { Content = "4. Максимум датчиков", Tag = "SensorsMax" });
                cmbPresets.Items.Add(new ComboBoxItem { Content = "5. Кривая затухания", Tag = "FadeCurve" });
                cmbPresets.Items.Add(new ComboBoxItem { Content = "6. Обмен порогов", Tag = "CalibrationSwap" });
                cmbPresets.SelectedIndex = 0;
            }

            // ------ Вкладка «Циклы» ------
            if (dgArray != null)
                dgArray.ItemsSource = _arrayData; // привязывает коллекцию _arrayData к таблице DataGrid

            if (cmbLoopMode != null)
            {
                cmbLoopMode.SelectionChanged += CmbLoopMode_SelectionChanged;

                // Обработка режима, выбранного по умолчанию
                CmbLoopMode_SelectionChanged(cmbLoopMode, null);
            }

            GenerateArrayInternal();

            GenerateArrayInternal();
        }

        // Плавное появление окна при запуске
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var root = (FrameworkElement)this.Content;
            root.Opacity = 0;
            var tr = new TranslateTransform(0, 30);
            root.RenderTransform = tr;

            var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(500));
            var slide = new DoubleAnimation(30, 0, TimeSpan.FromMilliseconds(500));

            root.BeginAnimation(OpacityProperty, fade);
            tr.BeginAnimation(TranslateTransform.YProperty, slide);
        }

        // Переключение вкладок — плавное появление содержимого
        private void MainTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.Source is TabControl tc && tc.SelectedItem is TabItem tab)
            {
                if (tab.Content is UIElement content)
                {
                    AnimateIn(content);
                }
            }
        }

        // Плавное появление элемента (сдвиг справа + прозрачность)
        private void AnimateIn(UIElement el)
        {
            el.Opacity = 0;
            var tr = new TranslateTransform(30, 0);
            el.RenderTransform = tr;

            var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(250));
            var slide = new DoubleAnimation(30, 0, TimeSpan.FromMilliseconds(250));

            el.BeginAnimation(OpacityProperty, fade);
            tr.BeginAnimation(TranslateTransform.XProperty, slide);
        }

        // Короткий пульс (мигание) элемента
        private void Pulse(UIElement el)
        {
            var anim = new DoubleAnimation(1, 0.3, TimeSpan.FromMilliseconds(120))
            {
                AutoReverse = true,
                RepeatBehavior = new RepeatBehavior(3)
            };
            el.BeginAnimation(OpacityProperty, anim);
        }

        // ----------------Циклы------------------//

        // Обновление текущего индекса и результата цикла
        private void UpdateCurrentValues()
        {
            txtCurrentJ.Text = _cycleService.J.ToString();
            txtCurrentRes.Text = _cycleService.Result.ToString();
        }

        // Проверяет корректность введенных пользователем параметров для генератора
        private bool ValidateInputParameters(out int n, out int min, out int max, out int t)
        {
            n = 0; min = 0; max = 0; t = 0;

            // 1. Проверка N (длина массива)
            if (!int.TryParse(txtN.Text, out n))
            {
                MessageBox.Show("Поле 'n' должно быть целым числом.", "Ошибка ввода", MessageBoxButton.OK, MessageBoxImage.Error);
                txtN.Focus();
                return false;
            }
            if (n <= 0)
            {
                MessageBox.Show("Длина массива 'n' должна быть больше нуля.", "Ошибка ввода", MessageBoxButton.OK, MessageBoxImage.Error);
                txtN.Focus();
                return false;
            }
            if (n > 10000) // Защита от зависания DataGrid
            {
                MessageBox.Show("Слишком большая длина массива (максимум 10 000).", "Ошибка ввода", MessageBoxButton.OK, MessageBoxImage.Error);
                txtN.Focus();
                return false;
            }

            // 2. Проверка Min и Max
            if (!int.TryParse(txtMin.Text, out min) || !int.TryParse(txtMax.Text, out max))
            {
                MessageBox.Show("Границы диапазона (min, max) должны быть целыми числами.", "Ошибка ввода", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            if (min >= max)
            {
                MessageBox.Show("Минимальное значение (min) должно быть строго меньше максимального (max).", "Ошибка ввода", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            // 3. Проверка порога T (только если выбран режим CountGreaterThanT)
            if (!int.TryParse(txtT.Text, out t))
            {
                MessageBox.Show("Порог 'T' должен быть целым числом.", "Ошибка ввода", MessageBoxButton.OK, MessageBoxImage.Error);
                txtT.Focus();
                return false;
            }

            return true;
        }

        // Генерация массива
        private void GenerateArrayInternal()
        {
            if (!ValidateInputParameters(out int n, out int min, out int max, out int t))
            {
                return; // Прерываем генерацию, если данные неверные
            }

            var rng = new Random();
            _arrayData.Clear();
            for (int i = 0; i < n; i++)
            {
                _arrayData.Add(new ArrayItem { Index = i, Value = rng.Next(min, max + 1) });
            }

            _loopInitialized = false;
            _initSnapshot = null;
            ResetIndicators();
            if (_selectedMode != CycleMode.None)
                UpdateWpDisplay();
        }

        // Выбор режима
        private void CmbLoopMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Извлекает Tag из выбранного элемента ComboBox
            string tag = (cmbLoopMode.SelectedItem as ComboBoxItem)?.Tag as string ?? "None";

            // Сопоставляет строку с элементом перечисления
            switch (tag)
            {
                case "PrefixSum": _selectedMode = CycleMode.PrefixSum; break;
                case "Count":
                case "CountGreaterThanT":
                    _selectedMode = CycleMode.CountGreaterThanT;
                    break;
                case "PrefixMax": _selectedMode = CycleMode.PrefixMax; break;
                default: _selectedMode = CycleMode.None; break;
            }

            _loopInitialized = false;
            _initSnapshot = null;

            UpdateInvariantDisplay();
            UpdateModeDescription();
            ResetIndicators();

            // Если выбран не "None", заново рассчитывает wp для нового тела цикла
            if (_selectedMode != CycleMode.None)
                UpdateWpDisplay();
        }
        private void UpdateModeDescription()
        {
            switch (_selectedMode)
            {
                case CycleMode.PrefixSum:
                    txtModeDescription.Text = "Считает суммарную освещённость за ночь — нужно для статистики энергопотребления.";
                    break;
                case CycleMode.CountGreaterThanT:
                    txtModeDescription.Text = "Подсчитывает, сколько раз свет превышал порог T — нужно для определения «дневных» интервалов.";
                    break;
                case CycleMode.PrefixMax:
                    txtModeDescription.Text = "Находит пиковое значение освещённости — нужно для калибровки максимальной яркости ночника.";
                    break;
                default:
                    txtModeDescription.Text = "— выберите режим —";
                    break;
            }
        }

        private void GenerateArray_Click(object sender, RoutedEventArgs e)
        {
            GenerateArrayInternal();
        }

        // Автоматическое выполнение цикла
        private async void RunLoop_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;

            if (_isRunning)
            {
                _runCts?.Cancel();
                return;
            }

            if (!EnsureInitialized()) return;

            _isRunning = true;
            if (btn != null) btn.Content = "⏸ Пауза";

            // Блокируем только кнопку Сброс
            SetResetButtonEnabled(sender, false);

            _runCts = new CancellationTokenSource();
            var token = _runCts.Token;

            try
            {
                while (!_cycleService.IsFinished && !token.IsCancellationRequested)
                {
                    DoOneStep();
                    await Task.Delay(350, token);
                }
            }
            catch (TaskCanceledException)
            {
                // Игнорируем отмену
            }
            finally
            {
                _isRunning = false;
                if (btn != null) btn.Content = "▶ Пуск";

                // Разблокируем кнопку Сброс
                SetResetButtonEnabled(sender, true);

                _runCts?.Dispose();
                _runCts = null;
            }
        }

        /// <summary>
        /// Обработчик кнопки «Справка» на вкладке «Циклы».
        /// </summary>
        private void Help_Click(object sender, RoutedEventArgs e)
        {
            string helpText =
                "📘 СПРАВКА: Вкладка «Анализ циклов прошивки ночника»\n\n" +

                "ЭТА ВКЛАДКА — часть экосистемы «Умного ночника». " +
                "Она позволяет формально доказать корректность циклических алгоритмов прошивки " +
                "(сумма показаний, счётчик превышений порога, поиск пика освещённости).\n\n" +

                "КАК ПОЛЬЗОВАТЬСЯ:\n" +
                "1. Выберите режим анализа в выпадающем списке.\n" +
                "2. Задайте параметры массива (n, min, max, порог T) и нажмите «Сгенерировать».\n" +
                "3. Нажимайте «Шаг» для пошагового прохода цикла или «Пуск» для автоматического выполнения.\n" +
                "4. Наблюдайте за изменением значений j и res, убыванием вариант-функции t и " +
                "индикаторами истинности инварианта.\n" +
                "5. По завершении цикла загорится зелёный индикатор постусловия.\n\n" +

                "ЧТО ОЗНАЧАЮТ ИНДИКАТОРЫ:\n" +
                "Зелёный — условие выполнено.\n" +
                "Красный — условие нарушено (ошибка в алгоритме).\n" +
                "Серый — ожидание проверки.\n\n" +

                "ФОРМАЛЬНЫЕ ПОНЯТИЯ:\n" +
                "• Инвариант (Inv) — условие, истинное до и после каждой итерации цикла.\n" +
                "• Вариант-функция (t) — целое неотрицательное значение, строго убывающее на каждом шаге. " +
                "Гарантирует завершение цикла.\n" +
                "• wp(S, Inv) — слабейшее предусловие, вычисляемое движком из ЛР-2.\n\n" +

                "ЧЕТЫРЕ УСЛОВИЯ ТОТАЛЬНОЙ КОРРЕКТНОСТИ:\n" +
                "1. Инициализация: Pre ⇒ Inv\n" +
                "2. Сохранение: (Inv ∧ B) ⇒ wp(S, Inv)\n" +
                "3. Выход: (Inv ∧ ¬B) ⇒ Post\n" +
                "4. Завершение: t ≥ 0 и t строго убывает\n\n" +

                "ЛЕГЕНДА НОЧНИКА:\n" +
                "• Сумма показаний — статистика энергопотребления за ночь.\n" +
                "• Счётчик превышений — определение «дневных» интервалов.\n" +
                "• Пик освещённости — калибровка максимальной яркости.";

            MessageBox.Show(helpText, "Справка по вкладке «Циклы»",
                            MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // Один шаг цикла
        private async void StepLoop_Click(object sender, RoutedEventArgs e)
        {
            // Если идет автозапуск, ставим его на паузу
            if (_isRunning)
            {
                _runCts?.Cancel();

                // Ждем, пока фоновый цикл полностью завершится (_isRunning станет false)
                while (_isRunning)
                {
                    await Task.Delay(10); // Небольшая пауза, чтобы не нагружать процессор
                }
            }

            if (!EnsureInitialized()) return;
            if (_cycleService.IsFinished) { UpdatePostStatus(); return; }

            DoOneStep();
        }

        // Сбрасывает выполнение цикла в начальное состояние
        private void ResetLoop_Click(object sender, RoutedEventArgs e)
        {
            if (_isRunning) return;
            _loopInitialized = false;
            _initSnapshot = null;
            ResetIndicators();
            UpdateInvariantDisplay();
            if (_selectedMode != CycleMode.None) UpdateWpDisplay();
        }

        // Блокирует кнопку Сброс во время автозапуска, оставляя Шаг активным
        private void SetResetButtonEnabled(object sender, bool isEnabled)
        {
            if (sender is Button runButton && runButton.Parent is Grid buttonGrid)
            {
                foreach (var child in buttonGrid.Children)
                {
                    if (child is Button btn && btn != runButton && btn.Content.ToString().Contains("Сброс"))
                    {
                        btn.IsEnabled = isEnabled;
                    }
                }
            }
        }

        /// <summary>Один шаг: проверка Inv до/после + обновление всех индикаторов.</summary>
        private void DoOneStep()
        {
            // Инвариант до шага
            bool invBefore = _cycleService.CheckInvariantLogic();
            lblInvBefore.Background = invBefore ? Brushes.Green : Brushes.Red;

            int oldT = _cycleService.VariantT;

            // Сам шаг
            _cycleService.Step();

            // Обновление текущего индекса и результата
            UpdateCurrentValues();

            // Инвариант после шага
            bool invAfter = _cycleService.CheckInvariantLogic();
            lblInvAfter.Background = invAfter ? Brushes.Green : Brushes.Red;

            UpdateVariantDisplay();
            UpdatePostStatus();

            // Лог убывания t
            int newT = _cycleService.VariantT;
            if (newT >= oldT || newT < 0)
            {
                txtVariant.Text += "  ⚠ t не убывает!";
            }
        }

        // Готовит сервис к работе при первом шаге
        private bool EnsureInitialized()
        {
            if (_selectedMode == CycleMode.None)
            {
                MessageBox.Show("Сначала выберите режим анализа во вкладке «Циклы».",
                    "Режим не выбран", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            // Проверяем, что текущие поля ввода все еще корректны
            if (!ValidateInputParameters(out _, out _, out _, out int threshold))
            {
                return false;
            }

            // Если массив отредактировали после инициализации — перезапускаем
            if (_loopInitialized && _initSnapshot != null && DataChangedSinceInit())
                _loopInitialized = false;

            if (!_loopInitialized)
            {
                int[] values = new int[_arrayData.Count];
                for (int i = 0; i < _arrayData.Count; i++) values[i] = _arrayData[i].Value;

                _cycleService.Initialize(_selectedMode, values, threshold);
                _initSnapshot = (int[])values.Clone();
                _loopInitialized = true;

                bool invOk = _cycleService.CheckInvariantLogic();
                lblInvBefore.Background = invOk ? Brushes.Green : Brushes.Red;
                lblInvAfter.Background = Brushes.Gray;

                UpdateCurrentValues();
                UpdateVariantDisplay();
                UpdatePostStatus();
                UpdateWpDisplay();
            }

            return true;
        }

        private bool DataChangedSinceInit()
        {
            if (_initSnapshot == null || _initSnapshot.Length != _arrayData.Count) return true;
            for (int i = 0; i < _arrayData.Count; i++)
                if (_arrayData[i].Value != _initSnapshot[i]) return true;
            return false;
        }

        // Обновление индикаторов
        private void UpdateInvariantDisplay()
        {
            switch (_selectedMode)
            {
                case CycleMode.PrefixSum:
                    txtInvHuman.Text = "res хранит сумму элементов a[0..j-1]";
                    txtInvFormula.Text = "res = Σ a[i] (i от 0 до j-1)  ∧  0 ≤ j ≤ n";
                    break;
                case CycleMode.CountGreaterThanT:
                    txtInvHuman.Text = "res хранит количество элементов a[0..j-1], превышающих T";
                    txtInvFormula.Text = "res = |{ i < j : a[i] > T }|  ∧  0 ≤ j ≤ n";
                    break;
                case CycleMode.PrefixMax:
                    txtInvHuman.Text = "res хранит максимум элементов a[0..j-1]";
                    txtInvFormula.Text = "res = max(a[0..j-1])  ∧  1 ≤ j ≤ n";
                    break;
                default:
                    txtInvHuman.Text = "— выберите режим —";
                    txtInvFormula.Text = "—";
                    break;
            }
        }

        private void UpdateVariantDisplay()
        {
            int n = _cycleService.N;
            int j = _cycleService.J;
            int t = _cycleService.VariantT;

            txtVariant.Text = $"t = n - j = {n} - {j} = {t}";
            pbVariant.Maximum = Math.Max(1, n);
            pbVariant.Value = Math.Max(0, t);
        }

        private void UpdatePostStatus()
        {
            if (_cycleService.IsFinished)
            {
                bool ok = _cycleService.CheckPostcondition();
                lblPostIndicator.Background = ok ? Brushes.Green : Brushes.Red;
                txtPostStatus.Text = ok ? " — Постусловие выполнено ✓" : " — Постусловие НЕ выполнено ✗";
            }
            else
            {
                lblPostIndicator.Background = Brushes.Gray;
                txtPostStatus.Text = " — цикл не завершён";
            }
        }

        private void ResetIndicators()
        {
            lblInvBefore.Background = Brushes.Gray;
            lblInvAfter.Background = Brushes.Gray;
            lblPostIndicator.Background = Brushes.Gray;
            txtPostStatus.Text = " — ожидание запуска";
            txtVariant.Text = "t = n - j = —";
            pbVariant.Value = 0;
            txtWpResult.Text = "— нажмите 'Шаг' для вычисления —";

            txtCurrentJ.Text = "0";
            txtCurrentRes.Text = "0";
        }

        private void UpdateWpDisplay()
        {
            Statement body = BuildLoopBodyForMode(_selectedMode);
            if (body == null) return;

            try
            {
                var engine = new Engine();
                Engine.Result result = engine.Calculate(body, new Variable("Inv"));

                txtWpResult.Text = $"(Inv ∧ j < n)  ⇒  wp(S, Inv)\n= {result.Final.Simplify().ToMathString()}";
                txtWpExplanation.Text = _cycleService.GetWpExplanation();
            }
            catch (Exception ex)
            {
                txtWpResult.Text = "Ошибка: " + ex.Message;
            }
        }

        // Тело цикла S для каждого режима (совпадает с GetWpExplanation)
        private Statement BuildLoopBodyForMode(CycleMode mode)
        {
            switch (mode)
            {
                case CycleMode.PrefixSum:
                    return new Sequence(new List<Statement>
            {
                new Assignment("res", new BinaryOperation(new Variable("res"), new Variable("a_j"), "+")),
                new Assignment("j",   new BinaryOperation(new Variable("j"),   new Constants(1),   "+"))
            });

                case CycleMode.CountGreaterThanT:
                    return new Sequence(new List<Statement>
            {
                new IfStatement(
                    new BinaryOperation(new Variable("a_j"), new Variable("T"), ">"),
                    new List<Statement>
                    {
                        new Assignment("res", new BinaryOperation(new Variable("res"), new Constants(1), "+"))
                    }),
                new Assignment("j", new BinaryOperation(new Variable("j"), new Constants(1), "+"))
            });

                case CycleMode.PrefixMax:
                    return new Sequence(new List<Statement>
            {
                new IfStatement(
                    new BinaryOperation(new Variable("a_j"), new Variable("res"), ">"),
                    new List<Statement>
                    {
                        new Assignment("res", new Variable("a_j"))
                    }),
                new Assignment("j", new BinaryOperation(new Variable("j"), new Constants(1), "+"))
            });

                default:
                    return null;
            }
        }

        #region
        // -----------------НОЧНИК-----------------
        // ОСНОВА ДЛЯ ВЫБОРА ОПЕРАЦИИ ИЗ СПИСКА
        private void OperationsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Проверяем, что пользователь действительно что-то выбрал
            if (OperationsListBox.SelectedItem != null)
            {
                //Очистка дочерних элементов перед созданием новых (удаление детей, ха-ха)
                FuncControlsPanel.Children.Clear();

                // Получаем выбранный элемент
                var selectedItem = OperationsListBox.SelectedItem;

                // Если в ListBox хранятся просто строки (как мы добавили в конструкторе),
                // то приводим его к строке и выводим в label1.
                // Если у вас там сложные объекты, нужно будет обращаться к их свойствам.
                string operationName = selectedItem.ToString();

                // Выводим название операции в заголовок
                label1.Content = operationName;

                // Сбрасываем старые результаты при выборе новой операции
                labelPreconditionValue.Content = "";
                labelPostconditionValue.Content = "";
                labelResult.Content = "";

                // Сбрасываем цвета квадратиков в серый
                labelPreconditionColor.Background = Brushes.Gray;
                labelPostconditionColor.Background = Brushes.Gray;

                //Создание дочерних элементов при выборе опр. действия
                switch (OperationsListBox.SelectedIndex)
                {
                    case 0:
                        pre_txt = "Питание есть (chk_power.IsChecked = true)\n" +
                            "Свет выключен (NightlightState.isOn = false)";
                        post_txt = "Свет включен (NightlightState.isOn = true)\n" +
                            "Если яркость была 0 (выключен) – яркость 50 (NightlightState.Brightness = 50)\n" +
                            "Если яркость была настроена ранее — значение сохраняется (NightlightState.Brightness не изменяется)";
                        ControlsForOn();
                        break;
                    case 1:
                        pre_txt = "Свет включен (NightlightState.isOn = true)\n" +
                            "Новая яркость не равна старой (newBrightness != oldBrightness)";
                        post_txt = "Яркость изменена (NightlightState.Brightness = newBrightness)\n" +
                            "Если яркость была настроена на 0 – выключение (NightlightState.isOn = false)\n" +
                            "Если яркость была настроена не на 0 — состояние сохраняется (NightlightState.isOn = true)";
                        ControlsForSet();
                        break;
                    case 2:
                        pre_txt = "Свет включен (NightlightState.isOn = true)\n" +
                            "Новый цвет отличается от прежнего (newColor != oldColor)";
                        post_txt = "Цвет изменен (NightlightState.currentColor = newColor)\n" +
                            "Включенное состояние сохраняется (NightlightState.isOn = true)";
                        ControlsForColor();
                        break;
                    case 4:
                        pre_txt = "Свет включен (NightlightState.isOn = true)\n";
                        post_txt = "Свет выключен (NightlightState.isOn = false)\n" +
                            "Сохранение настроенной яркости (NightlightState.Brightness не изменяется)";
                        ControlsForOff();
                        break;
                    default:
                        break;
                }
            }

            AnimateIn(label1);
            AnimateIn(FuncControlsPanel);
        }

        /// <summary>
        /// Создание элементов управления для функции включения
        /// </summary>
        private void ControlsForOn()
        {
            chk_power = new CheckBox
            {
                Content = "Питание есть", //Корректировка чекбокса
                FontSize = 14
            };

            chk_power.Checked += chk_power_select;
            chk_power.Unchecked += chk_power_select;

            FuncControlsPanel.Children.Add(chk_power);

            Label lbl_is_off = new Label
            {
                Content = "Свет должен быть выключен",
                FontSize = 14
            };
            FuncControlsPanel.Children.Add(lbl_is_off);
        }

        private void chk_power_select(object sender, EventArgs e)
        {
            if (chk_power.IsChecked == true)
            {
                PreWork();
                labelPostconditionColor.Background = Brushes.Gray;
                labelPostconditionValue.Content = $"";
                labelResult.Content = "";
            }
            else
            {
                PreDontWork("Нет питания");
                labelPostconditionColor.Background = Brushes.Gray;
                labelPostconditionValue.Content = $"";
                labelResult.Content = "Операция недоступна";
                if (newNightlight.isOn)
                    controller.TurnOff(newNightlight);
            }
        }

        /// <summary>
        /// Реакция лейбла на движение ползунка
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void sld_brightness_set(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            var slider = (Slider)sender;
            var parent = slider.Parent as Panel;
            if (parent == null) return;

            lbl_actuall_brightness.Content = $"{e.NewValue:0}%";

            if (!newNightlight.isOn)
            {
                PreDontWork("Ночник выключен");
                labelPostconditionColor.Background = Brushes.Gray;
                labelPostconditionValue.Content = $"";
                labelResult.Content = "Операция недоступна";
            }
            else if (sld_brightness.Value == newNightlight.Brightness)
            {
                PreDontWork("Яркость не изменена");
                labelPostconditionColor.Background = Brushes.Gray;
                labelPostconditionValue.Content = $"";
                labelResult.Content = "Операция недоступна";
            }
            else
            {
                PreWork();
                labelPostconditionColor.Background = Brushes.Gray;
                labelPostconditionValue.Content = $"";
                labelResult.Content = "";
            }
            ;
        }

        public void ControlsForColor()
        {
            Label lbl_is_on = new Label
            {
                Content = "Свет должен быть включен",
                FontSize = 14
            };
            FuncControlsPanel.Children.Add(lbl_is_on);

            var row = new Grid { Margin = new Thickness(0, 4, 0, 4) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            Label lbl_color = new Label
            {
                Content = "Новый цвет:",
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            Grid.SetColumn(lbl_color, 0);
            row.Children.Add(lbl_color);

            cmb_colors = new ComboBox();
            foreach (string s in colors)
                cmb_colors.Items.Add(s);

            int indColor;
            switch (newNightlight.currentColor)
            {
                case "warmwhite":
                    indColor = 0;
                    break;
                case "coldwhite":
                    indColor = 1;
                    break;
                case "white":
                    indColor = 2;
                    break;
                default:
                    indColor = 2;
                    break;
            }
            cmb_colors.SelectedIndex = indColor;

            cmb_colors.SelectionChanged += cmb_colors_selectionChanged;
            Grid.SetColumn(cmb_colors, 1);
            row.Children.Add(cmb_colors);

            FuncControlsPanel.Children.Add(row);
        }

        private void cmb_colors_selectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!newNightlight.isOn)
            {
                PreDontWork("Ночник выключен");
                labelPostconditionColor.Background = Brushes.Gray;
                labelPostconditionValue.Content = $"";
                labelResult.Content = "Операция недоступна";
            }
            else if (cmb_colors.SelectedItem.ToString().ToLower().Replace(" ", "") == newNightlight.currentColor)
            {
                PreDontWork("Цвет не изменён");
                labelPostconditionColor.Background = Brushes.Gray;
                labelPostconditionValue.Content = $"";
                labelResult.Content = "Операция недоступна";
            }
            else
            {
                PreWork();
                labelPostconditionColor.Background = Brushes.Gray;
                labelPostconditionValue.Content = $"";
                labelResult.Content = "";
            }
        }

        /// <summary>
        /// Создание элементов управления для функции настройки яркости
        /// </summary>
        private void ControlsForSet()
        {
            Label lbl_is_on = new Label
            {
                Content = "Свет должен быть включен",
                FontSize = 14
            };
            FuncControlsPanel.Children.Add(lbl_is_on);

            var row = new Grid { Margin = new Thickness(0, 4, 0, 4) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            Label lbl_brightness = new Label
            {
                Content = "Новая яркость:",
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            Grid.SetColumn(lbl_brightness, 0);
            row.Children.Add(lbl_brightness);

            sld_brightness = new Slider
            {
                Minimum = 0,
                Maximum = 100,
                Value = newNightlight.Brightness,
                TickFrequency = 1,
                IsSnapToTickEnabled = true,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(sld_brightness, 1);
            row.Children.Add(sld_brightness);

            lbl_actuall_brightness = new Label
            {
                Content = $"{newNightlight.Brightness}%",
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
                MinWidth = 48,
                Margin = new Thickness(8, 0, 0, 0)
            };
            Grid.SetColumn(lbl_actuall_brightness, 2);
            row.Children.Add(lbl_actuall_brightness);

            sld_brightness.ValueChanged += sld_brightness_set;

            FuncControlsPanel.Children.Add(row);
        }

        /// <summary>
        /// Создание элементов управления для функции выключения
        /// </summary>
        private void ControlsForOff()
        {
            Label lbl_is_on = new Label
            {
                Content = "Свет должен быть включен",
                FontSize = 14
            };
            FuncControlsPanel.Children.Add(lbl_is_on);
        }

        /// <summary>
        /// ОБРАБОТЧИК КНОПКИ "ВЫПОЛНИТЬ"
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Run_Click(object sender, RoutedEventArgs e)
        {
            switch (OperationsListBox.SelectedIndex)
            {
                case 0:
                    Run_On();
                    break;
                case 1:
                    Run_Set();
                    break;
                case 2:
                    Run_Color();
                    break;
                case 4:
                    Run_Off();
                    break;
                default:
                    break;
            }

            Pulse(labelResult);
            Pulse(labelPreconditionColor);
            Pulse(labelPostconditionColor);
        }

        /// <summary>
        /// Обработчик для функции включения
        /// </summary>
        private void Run_On()
        {
            //Проверка Pre
            bool havePower = chk_power.IsChecked == true;
            int oldBrightness = newNightlight.Brightness;
            if (!havePower)
            {
                PreDontWork("нет питания");
                PostDontWork("свет не был вкюлчён");
                return;
            }
            else if (newNightlight.isOn)
            {
                PreDontWork("свет уже включен");
                PostDontWork("свет не был вкюлчён");
                return;
            }
            else PreWork();

            //Настройка значений
            try
            {
                controller.TurnOn(newNightlight);
            }
            catch (Exception ex)
            {
                PreDontWork(ex.Message);
                PostDontWork(ex.Message);
                return;
            }

            //Проверка Post
            if (!newNightlight.isOn) PostDontWork("свет не был вкюлчён");
            else PostWork();
        }

        private void Run_Color()
        {
            //Проверка Pre
            string newColor = cmb_colors.SelectedItem.ToString().ToLower().Replace(" ", "");
            string oldColor = newNightlight.currentColor;
            if (!newNightlight.isOn)
            {
                PreDontWork("свет выключен");
                PostDontWork("яркость не была настроена");
                return;
            }
            else if (newColor == oldColor)
            {
                PreDontWork("не был выбран новый цвет");
                PostDontWork("цвет не был настроен");
                return;
            }
            else PreWork();

            //Настройка значений
            try
            { controller.SetColor(newNightlight, newColor); }
            catch (Exception ex)
            {
                PreDontWork(ex.Message);
                PostDontWork(ex.Message);
                return;
            }

            //Проверка Post
            if (newColor != newNightlight.currentColor) PostDontWork("цвет не был настроен");
            else PostWork();
        }

        /// <summary>
        /// Обработчик для функции изменения яркости
        /// </summary>
        private void Run_Set()
        {
            //Проверка Pre
            int newBrightness = (int)sld_brightness.Value;
            int oldBrightness = newNightlight.Brightness;
            if (!newNightlight.isOn)
            {
                PreDontWork("свет выключен");
                PostDontWork("яркость не была настроена");
                return;
            }
            else if (newBrightness == oldBrightness)
            {
                PreDontWork("новая яркость равна старой");
                PostDontWork("яркость не была настроена");
                return;
            }
            else PreWork();

            //Настройка значений
            try
            { controller.SetBrightness(newNightlight, newBrightness); }
            catch (Exception ex)
            {
                PreDontWork(ex.Message);
                PostDontWork(ex.Message);
                return;
            }

            //Проверка Post
            if (newBrightness != newNightlight.Brightness) PostDontWork("яркость не была настроена");
            else PostWork();
        }

        /// <summary>
        /// Обработчик для функции включения
        /// </summary>
        private void Run_Off()
        {
            //Проверка Pre
            if (!newNightlight.isOn)
            {
                PreDontWork("свет уже выключен");
                PostDontWork("свет не был выкюлчён");
                return;
            }
            else PreWork();

            //Настройка значений
            try
            { controller.TurnOff(newNightlight); }
            catch (Exception ex)
            {
                PreDontWork(ex.Message);
                PostDontWork(ex.Message);
                return;
            }

            //Проверка Post
            if (newNightlight.isOn) PostDontWork("свет не был выкюлчён");
            else PostWork();
        }

        /// <summary>
        /// При невыполненном Pre
        /// </summary>
        /// <param name="msg">Сообщение об ошибке</param>
        private void PreDontWork(string msg)
        {
            labelPreconditionColor.Background = Brushes.Red;
            labelPreconditionValue.Content = $"Ошибка: {msg}.";
        }

        /// <summary>
        /// При невыполненном Post
        /// </summary>
        /// <param name="msg"></param>
        private void PostDontWork(string msg)
        {
            labelPostconditionColor.Background = Brushes.Red;
            labelPostconditionValue.Content = $"Ошибка: {msg}.";
            labelResult.Content = "Операция была отменена.";
        }

        /// <summary>
        /// При выполненном Pre
        /// </summary>
        private void PreWork()
        {
            labelPreconditionColor.Background = Brushes.Green;
            labelPreconditionValue.Content = $"Выполнено.";
        }

        /// <summary>
        /// При выполненном Post
        /// </summary>
        private void PostWork()
        {
            labelPostconditionColor.Background = Brushes.Green;
            labelPostconditionValue.Content = $"Выполнено.";
            labelResult.Content = "Операция успешно завершена!";
        }

        // ОБРАБОТЧИК КНОПКИ "КОНТРАКТ
        private void Contract_Click(object sender, RoutedEventArgs e)
        {
            if (OperationsListBox.SelectedItem != null)
                MessageBox.Show($"{OperationsListBox.SelectedItem.ToString()}\n\n\n" +
                    $"pre:\n{pre_txt}\n\npost:\n{post_txt}",
                            $"Контракт операции {OperationsListBox.SelectedItem.ToString()}",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
            else MessageBox.Show($"Пожалуйста, выберете сначала операцию",
                            $"Операция не выбрана",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
        }


        #endregion fff

        #region
        // -----------------КОНСТРУКТОР ФРАГМЕНТА-----------------

        /// <summary>
        /// Выбор пресета из списка
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CmbPresets_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Защита от NullReferenceException при инициализации окна
            if (pnlTrace == null || lblWpResult == null || lblWpHuman == null || lblDefinedness == null) return;

            if (cmbPresets.SelectedItem != null)
            {
                ComboBoxItem item = (ComboBoxItem)cmbPresets.SelectedItem;
                string tag = (string)item.Tag;
                pnlTrace.Children.Clear();
                lblWpResult.Content = "-";
                lblWpHuman.Text = "-";
                lblDefinedness.Text = "-";
                currentHoareTriple = "";

                switch (tag)
                {
                    case "None":
                        txtCode.Text = "";
                        txtPost.Text = "";
                        break;
                    case "Max":
                        txtCode.Text = "if (x1 >= x2) { max = x1 } else { max = x2 }";
                        txtPost.Text = "max > 100";
                        break;
                    case "Sequence":
                        txtCode.Text = "x = x + 10; y = x + 1";
                        txtPost.Text = "(y == x - 9) && (x > 15)";
                        break;
                    case "Quadratic":
                        txtCode.Text = "if (D >= 0) { root = (-b + sqrt(D)) / (2 * a) } else { root = -1 }";
                        txtPost.Text = "(D >= 0 && a * root * root + b * root + c == 0) || (D < 0 && root == -1)";
                        break;
                    case "SensorsMax":
                        var sensorsData = NightLightPresets.GetSensorsMax();
                        txtCode.Text = sensorsData.program.ToMathString();
                        txtPost.Text = sensorsData.post.ToMathString();
                        break;

                    case "FadeCurve":
                        var fadeData = NightLightPresets.GetFadeCurve();
                        txtCode.Text = fadeData.program.ToMathString();
                        txtPost.Text = fadeData.post.ToMathString();
                        break;

                    case "CalibrationSwap":
                        var swapData = NightLightPresets.GetCalibrationSwap();
                        txtCode.Text = swapData.program.ToMathString();
                        txtPost.Text = swapData.post.ToMathString();
                        break;

                }
            }

        }

        /// <summary>
        /// Обработчик кнопки «Рассчитать wp»
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CalcWp_Click(object sender, RoutedEventArgs e)
        {
            pnlTrace.Children.Clear();
            lblWpResult.Content = "—";
            lblWpHuman.Text = "—";
            lblDefinedness.Text = "—";
            currentHoareTriple = "";

            try
            {
                // Парсим текст из интерфейса в объекты
                Statement program = ParseStatement(txtCode.Text);
                Expressions postcondition = ParseExpression(txtPost.Text);

                // Запускаем движок
                Engine engine = new Engine();
                Engine.Result result = engine.Calculate(program, postcondition);

                // Выводим пошаговый трейс
                foreach (var step in result.TraceSteps)
                {
                    pnlTrace.Children.Add(new TextBlock { Text = step });
                }

                // Выводим итоги
                var simplified = result.Final.Simplify();
                lblWpResult.Content = simplified.ToMathString();
                lblWpHuman.Text = simplified.ToHumanString();

                // Условия определённости
                if (result.DefinednessConditions.Count > 0)
                {
                    lblDefinedness.Text = string.Join(" И ", result.DefinednessConditions.Select(c => c.ToMathString()));
                    lblDefinedness.Foreground = Brushes.DarkGreen;
                }
                else
                {
                    lblDefinedness.Text = "Нет дополнительных ограничений (например, деления на ноль).";
                    lblDefinedness.Foreground = Brushes.Green;
                }

                // Сохраняем триаду для второй кнопки
                currentHoareTriple = $"{{{simplified.ToMathString()}}}\n\n{program.ToHumanString()}\n\n{{{txtPost.Text}}}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка парсинга или расчёта", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Обработчик кнопки «Показать триаду»
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ShowTriad_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(currentHoareTriple)) MessageBox.Show(currentHoareTriple, "Триада Хоара {Pre} P {Post}", MessageBoxButton.OK, MessageBoxImage.Information);
            else MessageBox.Show("Сначала нажмите кнопку «Рассчитать wp»", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        /// <summary>
        /// Точка входа для парсинга логических и арифметических выражений
        /// </summary>
        /// <param name="input"> выражение </param>
        /// <returns></returns>
        /// <exception cref="Exception"> Пустое выражение </exception>
        private Expressions ParseExpression(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) throw new Exception("Пустое выражение");
            var tokens = Tokenize(input);
            int pos = 0;
            return ParseOr(tokens, ref pos);
        }

        /// <summary>
        /// Лексический анализ: разбиение строки на токены (числа, переменные, операторы) с помощью регулярных выражений
        /// </summary>
        /// <param name="input"> выражение </param>
        /// <returns></returns>
        private List<string> Tokenize(string input)
        {
            // Паттерн ищет: числа, идентификаторы, составные операторы (>=, && и т.д.), одиночные символы
            var pattern = @"(\d+\.?\d*|[a-zA-Z_]\w*|>=|<=|==|!=|&&|\|\||[+\-*/()!><=]||sqrt\([a-zA-Z_]\w*\))";
            var matches = Regex.Matches(input, pattern);
            return matches.Cast<Match>().Select(m => m.Value).Where(v => v.Trim().Length > 0).ToList();
        }

        // Методы синтаксического анализа выражений (в порядке убывания приоритета)

        private Expressions ParseOr(List<string> t, ref int p) { var left = ParseAnd(t, ref p); while (p < t.Count && t[p] == "||") { p++; left = new BinaryOperation(left, ParseAnd(t, ref p), "||"); } return left; }
        private Expressions ParseAnd(List<string> t, ref int p) { var left = ParseEquality(t, ref p); while (p < t.Count && t[p] == "&&") { p++; left = new BinaryOperation(left, ParseEquality(t, ref p), "&&"); } return left; }
        private Expressions ParseEquality(List<string> t, ref int p) { var left = ParseRelational(t, ref p); while (p < t.Count && (t[p] == "==" || t[p] == "!=")) { string op = t[p++]; left = new BinaryOperation(left, ParseRelational(t, ref p), op); } return left; }
        private Expressions ParseRelational(List<string> t, ref int p) { var left = ParseAdditive(t, ref p); while (p < t.Count && (t[p] == ">" || t[p] == "<" || t[p] == ">=" || t[p] == "<=")) { string op = t[p++]; left = new BinaryOperation(left, ParseAdditive(t, ref p), op); } return left; }
        private Expressions ParseAdditive(List<string> t, ref int p) { var left = ParseMultiplicative(t, ref p); while (p < t.Count && (t[p] == "+" || t[p] == "-")) { string op = t[p++]; left = new BinaryOperation(left, ParseMultiplicative(t, ref p), op); } return left; }
        private Expressions ParseMultiplicative(List<string> t, ref int p) { var left = ParseUnary(t, ref p); while (p < t.Count && (t[p] == "*" || t[p] == "/")) { string op = t[p++]; left = new BinaryOperation(left, ParseUnary(t, ref p), op); } return left; }

        private Expressions ParseUnary(List<string> t, ref int p)
        {
            if (p < t.Count && (t[p] == "-" || t[p] == "!" || t[p] == "abs" || t[p] == "sqrt"))
            {
                string op = t[p++];
                return new UnaryOperation(ParseUnary(t, ref p), op);
            }
            return ParsePrimary(t, ref p);
        }

        private Expressions ParsePrimary(List<string> t, ref int p)
        {
            if (p >= t.Count) throw new Exception("Неожиданный конец выражения");

            string token = t[p++];

            // Рекурсивный вызов для выражений в скобках (сброс приоритета)
            if (token == "(")
            { var expr = ParseOr(t, ref p);
                if (p < t.Count && t[p] == ")") p++;
                return expr;
            }

            // Если токен является числом
            if (double.TryParse(token, out double num)) return new Constants(num);

            // Иначе считаем токен именем переменной
            return new Variable(token);
        }

        // Методы синтаксического анализа операторов (Statements)

        /// <summary>
        /// Разделяет код на отдельные операторы по символу ';' или распознаёт блок 'if'.
        /// </summary>
        /// <param name="input"> выражение </param>
        /// <returns></returns>
        /// <exception cref="Exception"> Пустая строка </exception>
        private Statement ParseStatement(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) throw new Exception("Пустая строка");
            var statements = new List<Statement>();
            int i = 0;
            while (i < input.Length)
            {
                // Пропуск пробельных символов
                while (i < input.Length && char.IsWhiteSpace(input[i])) i++;
                if (i >= input.Length) break;

                // Если оператор начинается с 'if', отдаём парсинг специализированному методу
                if (input.Substring(i).StartsWith("if"))
                {
                    int start = i; int braceCount = 0; bool inIf = false;

                    // Ищем парную закрывающую скобку для всего блока if-else
                    while (i < input.Length)
                    {
                        if (input[i] == '{') { braceCount++; inIf = true; }
                        if (input[i] == '}') braceCount--;
                        i++;
                        if (inIf && braceCount == 0) break;
                    }
                    statements.Add(ParseIfStatement(input.Substring(start, i - start).Trim()));
                }
                else
                {
                    // Иначе это присваивание, ищем разделитель ';'
                    int start = i;
                    while (i < input.Length && input[i] != ';') i++;
                    string stmtStr = input.Substring(start, i - start).Trim();
                    if (!string.IsNullOrEmpty(stmtStr)) statements.Add(ParseAssignment(stmtStr));
                    i++; // Пропускаем символ ';'
                }
            }
            // Если оператор один, возвращаем его. Если несколько, оборачиваем в Sequence
            return statements.Count == 1 ? statements[0] : new Sequence(statements);
        }

        /// <summary>
        /// Парсер условного оператора. Извлекает условие и блоки кода с помощью регулярных выражений
        /// </summary>
        /// <param name="input"> выражение </param>
        /// <returns></returns>
        /// <exception cref="Exception"> Ошибка синтаксиса </exception>
        private Statement ParseIfStatement(string input)
        {
            // Извлекаем условие в () и содержимое в {}
            var condMatch = Regex.Match(input, @"if\s*\((.*?)\)\s*\{(.*)\}");
            if (!condMatch.Success) throw new Exception("Ошибка синтаксиса if. Проверьте скобки");

            string condStr = condMatch.Groups[1].Value.Trim();
            string inner = condMatch.Groups[2].Value.Trim();
            var condition = ParseExpression(condStr);

            // Поиск ветки else (упрощенный поиск по подстроке)
            int elseIdx = inner.LastIndexOf("} {");
            Statement trueStmt, falseStmt;

            if (elseIdx > 0)
            {
                // Разделяем на true и false блоки и рекурсивно парсим их
                trueStmt = ParseStatement(inner.Substring(0, elseIdx).Trim());
                falseStmt = ParseStatement(inner.Substring(elseIdx + 8).TrimEnd('}', ' ').Trim());
            }
            else
            {
                // Если else нет, false-блок остается пустым
                trueStmt = ParseStatement(inner.TrimEnd('}', ' ').Trim());
                falseStmt = new Sequence(new List<Statement>());
            }

            // Нормализация: если результат парсинга - Sequence, берем его список, иначе создаем список из одного элемента
            return new IfStatement(condition, trueStmt is Sequence s1 ? s1.Statements : new List<Statement> { trueStmt }, falseStmt is Sequence s2 ? s2.Statements : new List<Statement> { falseStmt });
        }

        /// <summary>
        /// Парсер оператора присваивания
        /// </summary>
        /// <param name="input"> выражение </param>
        /// <returns></returns>
        /// <exception cref="Exception"> Неверный формат </exception>
        private Statement ParseAssignment(string input)
        {
            var parts = input.Split(new[] { ":=", "=" }, StringSplitOptions.None);

            if (parts.Length != 2) throw new Exception($"Неверный формат присваивания");
            return new Assignment(parts[0].Trim(), ParseExpression(parts[1].Trim()));
        }
        #endregion

        
    
    }
    /// <summary>
    /// Простая обёртка для привязки DataGrid на вкладке «Циклы».
    /// </summary>
    public class ArrayItem
    {
        public int Index { get; set; }
        public int Value { get; set; }
    }
}