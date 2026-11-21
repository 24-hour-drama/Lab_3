using Library.Lab2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Library.Lab3
{
    /// <summary>
    /// Анализ и пошаговое выполнение циклов
    /// </summary>
    public class CycleAnalyzerService
    {
        private int[] _array;
        private int _j;
        private int _result;
        private int _n;
        private int _t;
        private int _threshold;

        /// <summary>
        /// Режим
        /// </summary>
        public CycleMode CurrentMode { get; private set; }

        /// <summary>
        /// Текущий индекс
        /// </summary>
        public int J => _j;

        /// <summary>
        /// Результат
        /// </summary>
        public int Result => _result;

        /// <summary>
        /// Длина массива
        /// </summary>
        public int N => _n;

        /// <summary>
        /// Значение вариант-функции(t = n - j)
        /// </summary>
        public int T => _threshold;

        /// <summary>
        /// Порог для режима CountGreaterThanT
        /// </summary>
        public int VariantT => _t;

        /// <summary>
        /// Признак завершения цикла
        /// </summary>
        public bool IsFinished => _j >= _n;

        /// <summary>
        /// Инициализация состояния перед началом цикла (блок init).
        /// </summary>
        /// <param name="mode">Режим цикла</param>
        /// <param name="array">Массив данных</param>
        /// <param name="threshold">Пороговое значение</param>
        public void Initialize(CycleMode mode, int[] array, int threshold)
        {
            CurrentMode = mode;
            _array = (int[])array.Clone();
            _n = _array.Length;
            _threshold = threshold;
            _j = 0;
            _result = 0;

            if (mode == CycleMode.PrefixMax && _n > 0)
            {
                _result = _array[0];
                _j = 1;
            }

            UpdateVariant();
        }

        /// <summary>
        /// Рассчёт значения вариант-функции
        /// </summary>
        private void UpdateVariant()
        {
            _t = _n - _j;
        }

        /// <summary>
        /// Выполнение одного шага
        /// </summary>
        /// <returns>true - цикл можно продолжать; false - цикл завершён</returns>
        public bool Step()
        {
            if (IsFinished) return false;

            System.Diagnostics.Debug.Assert(CheckInvariantLogic(), "Инвариант нарушен перед шагом!");

            int oldT = _t;

            if (CurrentMode == CycleMode.PrefixSum)
            {
                _result += _array[_j];
                _j++;
            }
            else if (CurrentMode == CycleMode.CountGreaterThanT)
            {
                if (_array[_j] > _threshold) _result++;
                _j++;
            }
            else if (CurrentMode == CycleMode.PrefixMax)
            {
                if (_array[_j] > _result) _result = _array[_j];
                _j++;
            }

            UpdateVariant();

            System.Diagnostics.Debug.Assert(CheckInvariantLogic(), "Инвариант нарушен после шага");
            System.Diagnostics.Debug.Assert(_t >= 0 && _t < oldT, "Вариант-функция не убывает или стала отрицательной");

            return !IsFinished;
        }

        /// <summary>
        /// Проверка истинности инварианта (Inv)
        /// </summary>
        /// <returns>true - инваниант истинен; false - инвариант нарушен</returns>
        public bool CheckInvariantLogic()
        {
            if (_j < 0 || _j > _n) return false;

            if (CurrentMode == CycleMode.PrefixSum)
            {
                int expectedSum = 0;
                for (int i = 0; i < _j; i++) expectedSum += _array[i];
                return _result == expectedSum;
            }
            if (CurrentMode == CycleMode.CountGreaterThanT)
            {
                int expectedCount = 0;
                for (int i = 0; i < _j; i++) if (_array[i] > _threshold) expectedCount++;
                return _result == expectedCount;
            }
            if (CurrentMode == CycleMode.PrefixMax)
            {
                if (_j == 0) return false;
                int expectedMax = _array[0];
                for (int i = 1; i < _j; i++) if (_array[i] > expectedMax) expectedMax = _array[i];
                return _result == expectedMax;
            }
            return false;
        }

        /// <summary>
        /// Проверка истинности постусловия (Post)
        /// </summary>
        /// <returns></returns>
        public bool CheckPostcondition()
        {
            if (!IsFinished) return false;

            switch (CurrentMode)
            {
                case CycleMode.PrefixSum:
                    return _result == _array.Sum();
                case CycleMode.CountGreaterThanT:
                    return _result == _array.Count(x => x > _threshold);
                case CycleMode.PrefixMax:
                    return _n > 0 && _result == _array.Max();
                default:
                    return false;
            }
        }
    }
}
