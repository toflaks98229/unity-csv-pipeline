using NUnit.Framework;

namespace CsvPipeline.Tests
{
    /// <summary>
    /// 계획을 상태 하나로 줄이는 규칙을 확인합니다.
    /// 목록의 아이콘·색·정렬·필터가 모두 이 판정 하나에 매여 있어, 틀리면 화면 전체가 함께 틀립니다.
    /// </summary>
    public sealed class CsvPlanStatusTests
    {
        /// <summary>빈 계획을 만듭니다.</summary>
        /// <returns>계획입니다.</returns>
        private static CsvImportPlan Plan() => new CsvImportPlan("Widgets.csv", "WidgetData");

        /// <summary>바뀌는 것이 없으면 Ok입니다.</summary>
        [Test]
        public void 바뀌는_것이_없으면_Ok다()
        {
            Assert.AreEqual(CsvPlanState.Ok, CsvPlanStatus.Of(Plan()));
        }

        /// <summary>건너뜀만 있는 것은 바뀌는 것이 아닙니다.</summary>
        [Test]
        public void 건너뜀만_있으면_Ok다()
        {
            CsvImportPlan plan = Plan();
            plan.Add(CsvChangeKind.Skip, null, 2, "식별자가 비어 있습니다.");

            Assert.AreEqual(CsvPlanState.Ok, CsvPlanStatus.Of(plan));
        }

        /// <summary>만들거나 갱신할 것이 있으면 Changed입니다.</summary>
        [Test]
        public void 만들거나_갱신하면_Changed다()
        {
            CsvImportPlan create = Plan();
            create.Add(CsvChangeKind.Create, "Assets/A.asset");
            Assert.AreEqual(CsvPlanState.Changed, CsvPlanStatus.Of(create));

            CsvImportPlan update = Plan();
            update.Add(CsvChangeKind.Update, "Assets/A.asset");
            Assert.AreEqual(CsvPlanState.Changed, CsvPlanStatus.Of(update));
        }

        /// <summary>
        /// 사라지는 것이 하나라도 있으면 Removing입니다. 생성이 아무리 많아도 그쪽이 앞섭니다.
        /// 삭제만 되돌릴 수 없기 때문입니다.
        /// </summary>
        [Test]
        public void 삭제가_생성보다_앞선다()
        {
            CsvImportPlan plan = Plan();
            for (int i = 0; i < 20; i++) plan.Add(CsvChangeKind.Create, $"Assets/A{i}.asset");
            plan.Add(CsvChangeKind.Delete, "Assets/Z.asset");

            Assert.AreEqual(CsvPlanState.Removing, CsvPlanStatus.Of(plan));
        }

        /// <summary>보존은 삭제가 아닙니다. 참조가 남아 지우지 않기로 한 것이라 경고가 아닙니다.</summary>
        [Test]
        public void 보존은_삭제로_치지_않는다()
        {
            CsvImportPlan plan = Plan();
            plan.Add(CsvChangeKind.Preserve, "Assets/Z.asset", 0, "참조가 남았습니다.");

            Assert.AreEqual(CsvPlanState.Ok, CsvPlanStatus.Of(plan));
        }

        /// <summary>오류는 삭제보다 앞섭니다.</summary>
        [Test]
        public void 오류가_삭제보다_앞선다()
        {
            CsvImportPlan plan = Plan();
            plan.Add(CsvChangeKind.Delete, "Assets/Z.asset");
            plan.Issues.Add(new CsvIssue(CsvIssueSeverity.Error, "열 'HP'이(가) 없습니다."));

            Assert.AreEqual(CsvPlanState.Problem, CsvPlanStatus.Of(plan));
        }

        /// <summary>경고만 있어도 손볼 것으로 봅니다. 목록에서 묻히면 안 됩니다.</summary>
        [Test]
        public void 경고도_Problem이다()
        {
            CsvImportPlan plan = Plan();
            plan.Issues.Add(new CsvIssue(CsvIssueSeverity.Warning, "에셋이 둘입니다."));

            Assert.AreEqual(CsvPlanState.Problem, CsvPlanStatus.Of(plan));
        }

        /// <summary>계획을 못 세웠으면 Blocked입니다. 무슨 일이 일어날지 알 수 없는 상태입니다.</summary>
        [Test]
        public void 계획을_못_세우면_Blocked다()
        {
            CsvImportPlan plan = Plan();
            plan.Unsupported = "표 파일을 찾지 못했습니다.";

            Assert.AreEqual(CsvPlanState.Blocked, CsvPlanStatus.Of(plan));
        }

        /// <summary>null도 Blocked로 봅니다. 화면이 예외로 무너지지 않아야 합니다.</summary>
        [Test]
        public void null도_Blocked다()
        {
            Assert.AreEqual(CsvPlanState.Blocked, CsvPlanStatus.Of(null));
        }

        /// <summary>
        /// 상태 값의 크기가 곧 목록의 정렬 순서입니다.
        /// 이 순서가 흐트러지면 정작 봐야 할 표가 아래로 묻힙니다.
        /// </summary>
        [Test]
        public void 무거운_상태일수록_값이_크다()
        {
            Assert.Less((int)CsvPlanState.Ok, (int)CsvPlanState.Changed);
            Assert.Less((int)CsvPlanState.Changed, (int)CsvPlanState.Removing);
            Assert.Less((int)CsvPlanState.Removing, (int)CsvPlanState.Problem);
            Assert.Less((int)CsvPlanState.Problem, (int)CsvPlanState.Blocked);
        }

        /// <summary>손볼 것이 있는 상태를 가립니다.</summary>
        [Test]
        public void 손볼_것을_가린다()
        {
            Assert.IsTrue(CsvPlanStatus.NeedsAttention(CsvPlanState.Problem));
            Assert.IsTrue(CsvPlanStatus.NeedsAttention(CsvPlanState.Blocked));

            Assert.IsFalse(CsvPlanStatus.NeedsAttention(CsvPlanState.Removing));
            Assert.IsFalse(CsvPlanStatus.NeedsAttention(CsvPlanState.Changed));
            Assert.IsFalse(CsvPlanStatus.NeedsAttention(CsvPlanState.Ok));
        }
    }
}
