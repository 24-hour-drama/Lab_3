using Library;
using Library.Lab3;
using Library.Lab2;
using System;
using Xunit;

namespace Tests
{
    /// <summary>
    /// Тесты для класса CycleAnalyzerService
    /// </summary>
    public class CycleAnalyzerServiceTests
    {
        private record StepSnapshot(int StepNumber, int JBefore, int JAfter, int ResultBefore, int ResultAfter, int TBefore, int TAfter, bool InvBefore, bool InvAfter, int HighlightIndexAfter);

        // Метод для проверки каждого шага цикла
        private static (List<StepSnapshot> History, List<int> VariantTrace) ExecuteEndToEnd(CycleAnalyzerService service, CycleMode mode, int[] inputArray, int threshold)
        {
            service.Initialize(mode, inputArray, threshold);

            Assert.Equal(mode, service.CurrentMode);
            Assert.Equal(inputArray.Length, service.N);
            Assert.Equal(threshold, service.T);
            Assert.True(service.VariantT >= 0);

            if (mode == CycleMode.PrefixMax && inputArray.Length > 0)
            {
                Assert.Equal(1, service.J);
                Assert.Equal(inputArray[0], service.Result);
                Assert.Equal(Math.Max(0, inputArray.Length - 1), service.VariantT);
            }
            else if (mode != CycleMode.None)
            {
                Assert.Equal(0, service.J);
                Assert.Equal(0, service.Result);
                Assert.Equal(inputArray.Length, service.VariantT);
            }

            var viewItems = service.GetArrayForView();
            Assert.Equal(inputArray.Length, viewItems.Count);
            for (int i = 0; i < inputArray.Length; i++)
            {
                Assert.Equal(i, viewItems[i].Index);
                Assert.Equal(inputArray[i], viewItems[i].Value);
            }

            var history = new List<StepSnapshot>();
            var variantTrace = new List<int> { service.VariantT };
            int stepNum = 0;

            while (!service.IsFinished)
            {
                stepNum++;
                int jBefore = service.J;
                int resBefore = service.Result;
                int tBefore = service.VariantT;
                bool invBefore = service.CheckInvariantLogic();

                Assert.True(invBefore);
                Assert.True(tBefore > 0);
                Assert.False(service.CheckPostcondition());
                Assert.Equal(jBefore, service.GetCurrentIndexForHighlight());

                bool canContinue = service.Step();

                int jAfter = service.J;
                int resAfter = service.Result;
                int tAfter = service.VariantT;
                bool invAfter = service.CheckInvariantLogic();

                Assert.True(tAfter >= 0);
                Assert.True(tAfter < tBefore);
                Assert.True(invAfter);

                int expectedHighlightAfter = service.IsFinished ? -1 : jAfter;
                Assert.Equal(expectedHighlightAfter, service.GetCurrentIndexForHighlight());
                Assert.Equal(!service.IsFinished, canContinue);

                history.Add(new StepSnapshot(stepNum, jBefore, jAfter, resBefore, resAfter, tBefore, tAfter, invBefore, invAfter, expectedHighlightAfter));
                variantTrace.Add(tAfter);
            }

            Assert.True(service.IsFinished);
            Assert.Equal(inputArray.Length, service.J);
            Assert.Equal(0, service.VariantT);
            Assert.Equal(-1, service.GetCurrentIndexForHighlight());
            Assert.True(service.CheckInvariantLogic());
            Assert.True(service.CheckPostcondition());
            return (history, variantTrace);
        }

        // проверка суммы префикса
        [Theory]
        [InlineData(new[] { 5 })]
        [InlineData(new[] { 1, 2, 3, 4, 5 })]
        [InlineData(new[] { -10, 25, 0, -5, 12, 3 })]
        [InlineData(new[] { 100, -100, 50, -50, 7, 8, 9, 1 })]
        public void EndToEnd_PrefixSum_FullLifecycle_MatchesPostAndVariant(int[] array)
        {
            var service = new CycleAnalyzerService();
            var (history, trace) = ExecuteEndToEnd(service, CycleMode.PrefixSum, array, threshold: 0);
            Assert.Equal(array.Length, history.Count);
            Assert.Equal(array.Sum(), service.Result);
            for (int i = 0; i < trace.Count - 1; i++)
            {
                Assert.True(trace[i] > trace[i + 1]);
                Assert.Equal(1, trace[i] - trace[i + 1]);
            }
            Assert.Equal(0, trace[^1]);
        }
        
        // Проверка счетчика элементов больше порога
        [Theory]
        [InlineData(new[] { 2 }, 1, 1)]
        [InlineData(new[] { 2 }, 5, 0)]
        [InlineData(new[] { 1, 6, 3, 10, 4, 8 }, 4, 3)]
        [InlineData(new[] { -5, -1, 0, 2, 15 }, 0, 2)]
        [InlineData(new[] { 7, 7, 7, 7 }, 7, 0)]
        public void EndToEnd_CountGreaterThanT_FullLifecycle_MatchesPostAndVariant(int[] array, int threshold, int expectedCount)
        {
            var service = new CycleAnalyzerService();
            var (history, trace) = ExecuteEndToEnd(service, CycleMode.CountGreaterThanT, array, threshold);

            Assert.Equal(array.Length, history.Count);
            Assert.Equal(expectedCount, service.Result);
            Assert.Equal(array.Count(x => x > threshold), service.Result);
            Assert.Equal(0, trace[^1]);
        }

        //поиск максимума префикса
        [Theory]
        [InlineData(new[] { 42 }, 42)]
        [InlineData(new[] { 10, 3, 25, 7, 19 }, 25)]
        [InlineData(new[] { -3, -9, -1, -20, -2 }, -1)]
        [InlineData(new[] { 5, 5, 5, 5 }, 5)]
        [InlineData(new[] { 100, 1, 2, 3, 4, 5 }, 100)]
        public void EndToEnd_PrefixMax_FullLifecycle_MatchesPostAndVariant(int[] array, int expectedMax)
        {
            var service = new CycleAnalyzerService();
            var (history, trace) = ExecuteEndToEnd(service, CycleMode.PrefixMax, array, threshold: 0);

            Assert.Equal(Math.Max(0, array.Length - 1), history.Count);
            Assert.Equal(expectedMax, service.Result);
            Assert.Equal(array.Max(), service.Result);
            Assert.Equal(0, trace[^1]);
        }

        //проверка смены режима
        [Fact]
        public void Reinitialize_And_ResetWorkflow_RestartsCleanlyWithoutStaleState()
        {
            var service = new CycleAnalyzerService();

            service.Initialize(CycleMode.PrefixSum, new[] { 10, 20, 30 }, threshold: 5);
            Assert.True(service.Step());
            Assert.Equal(10, service.Result);
            Assert.Equal(1, service.J);
            Assert.Equal(2, service.VariantT);

            service.Initialize(CycleMode.CountGreaterThanT, new[] { 2, 8, 12, 1 }, threshold: 5);
            Assert.Equal(CycleMode.CountGreaterThanT, service.CurrentMode);
            Assert.Equal(0, service.J);
            Assert.Equal(0, service.Result);
            Assert.Equal(4, service.VariantT);
            Assert.False(service.IsFinished);
            Assert.False(service.CheckPostcondition());

            var (_, trace) = ExecuteEndToEnd(service, CycleMode.CountGreaterThanT, new[] { 2, 8, 12, 1 }, threshold: 5);
            Assert.Equal(2, service.Result);
            Assert.Equal(new[] { 4, 3, 2, 1, 0 }, trace);
        }
        //проверка когда цикл закончен
        [Fact]
        public void StepWhenAlreadyFinished_IsSafeNoOpAndKeepsPostTrue()
        {
            var service = new CycleAnalyzerService();
            service.Initialize(CycleMode.PrefixSum, new[] { 4, 6 }, threshold: 0);

            while (!service.IsFinished)
            {
                service.Step();
            }

            int finalResult = service.Result;
            int finalJ = service.J;
            int finalT = service.VariantT;

            bool canContinueAfterFinish = service.Step();

            Assert.False(canContinueAfterFinish);
            Assert.True(service.IsFinished);
            Assert.Equal(finalResult, service.Result);
            Assert.Equal(finalJ, service.J);
            Assert.Equal(finalT, service.VariantT);
            Assert.Equal(-1, service.GetCurrentIndexForHighlight());
            Assert.True(service.CheckPostcondition());
        }

        //проверка подключения движка engine
        [Fact]
        public void WpEngineIntegration_AllModes_ProducesValidNonEmptyWpAndExplanation()
        {
            var service = new CycleAnalyzerService();
            var engine = new Engine();
            var invExpr = new Variable("Inv");

            foreach (var mode in new[] { CycleMode.PrefixSum, CycleMode.CountGreaterThanT, CycleMode.PrefixMax })
            {
                service.Initialize(mode, new[] { 3, 7, 2, 9 }, threshold: 4);

                string explanation = service.GetWpExplanation();
                Assert.False(string.IsNullOrWhiteSpace(explanation));
                Assert.Contains("Inv", explanation);

                Statement body = mode switch
                {
                    CycleMode.PrefixSum => new Sequence(new List<Statement>{new Assignment("res", new BinaryOperation(new Variable("res"), new Variable("a_j"), "+")),
                                                        new Assignment("j", new BinaryOperation(new Variable("j"), new Constants(1), "+"))}),
                    CycleMode.CountGreaterThanT => new Sequence(new List<Statement>{new IfStatement(new BinaryOperation(new Variable("a_j"), new Variable("T"), ">"),
                                                                new List<Statement>{new Assignment("res", new BinaryOperation(new Variable("res"), new Constants(1), "+"))}),
                                                                new Assignment("j", new BinaryOperation(new Variable("j"), new Constants(1), "+"))}),
                    CycleMode.PrefixMax => new Sequence(new List<Statement>{new IfStatement(new BinaryOperation(new Variable("a_j"), new Variable("res"), ">"),
                                                        new List<Statement>{new Assignment("res", new Variable("a_j"))}),
                                                        new Assignment("j", new BinaryOperation(new Variable("j"), new Constants(1), "+"))}),
                    _ => throw new ArgumentOutOfRangeException(nameof(mode))
                };

                var wpResult = engine.Calculate(body, invExpr);
                Assert.NotNull(wpResult);
                Assert.NotNull(wpResult.Final);
                Assert.False(string.IsNullOrWhiteSpace(wpResult.Final.ToString()));
            }
        }

        // пустой массив
        [Fact]
        public void BoundaryCase_PrefixMax_WithEmptyArray_IsDocumentedEdgeCase()
        {
            var service = new CycleAnalyzerService();
            service.Initialize(CycleMode.PrefixMax, Array.Empty<int>(), threshold: 0);

            Assert.Equal(0, service.N);
            Assert.Equal(0, service.J);
            Assert.Equal(0, service.VariantT);
            Assert.False(service.CheckInvariantLogic());
            Assert.False(service.CheckPostcondition());
        }
    }

}
