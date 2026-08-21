using NUnit.Framework;
using UnityEngine;

namespace CsvPipeline.Tests
{
    /// <summary>
    /// 화면의 <b>눈에 보이지 않는 판단</b>을 봅니다 — 색이 읽히는가, 키가 무슨 뜻인가, 무엇을 감추는가.
    /// <para>
    /// 셋 다 그리기 안에 두면 확인할 방법이 없어집니다. IMGUI 는 컴파일로 아무것도 보증하지 않고,
    /// 화면이 없는 배치 실행에서는 그려 볼 수조차 없기 때문입니다. 그래서 규칙만 떼어 내 검사합니다.
    /// </para>
    /// </summary>
    public sealed class CsvEditorUxTests
    {
        // ====================================================================================================
        // 색 — Unity Editor Design System US-0173 · US-0174
        // ====================================================================================================

        /// <summary>같은 색의 명암비는 1, 검정과 흰색은 21입니다. 계산이 맞는지 못 박습니다.</summary>
        [Test]
        public void 명암비_계산이_기준값과_맞는다()
        {
            Assert.AreEqual(1f, CsvPalette.ContrastRatio(Color.gray, Color.gray), 0.001f);
            Assert.AreEqual(21f, CsvPalette.ContrastRatio(Color.black, Color.white), 0.01f);
        }

        /// <summary>반투명한 색을 바탕에 겹치면 불투명해집니다.</summary>
        [Test]
        public void 겹치면_불투명해진다()
        {
            Color result = CsvPalette.Over(new Color(1f, 1f, 1f, 0.5f), Color.black);

            Assert.AreEqual(1f, result.a, 0.001f);
            Assert.AreEqual(0.5f, result.r, 0.001f);
        }

        /// <summary>
        /// 글자로 쓰는 색은 <b>두 스킨 모두에서</b> 4.5:1 을 넘겨야 합니다. (US-0173)
        /// 한 줄 걸러 깔리는 바탕까지 봐야 불리한 쪽을 놓치지 않습니다.
        /// </summary>
        [TestCase(true)]
        [TestCase(false)]
        public void 글자색이_읽히는_명암비를_넘는다(bool dark)
        {
            AssertReadable("Muted", CsvPalette.Muted(dark), dark, CsvPalette.TextContrast);
            AssertReadable("Accent", CsvPalette.Accent(dark), dark, CsvPalette.TextContrast);
            AssertReadable("Warning", CsvPalette.Warning(dark), dark, CsvPalette.TextContrast);
            AssertReadable("Danger", CsvPalette.Danger(dark), dark, CsvPalette.TextContrast);
        }

        /// <summary>
        /// 상태를 나르는 색은 글자로 쓰이지 않을 때에도 3:1 을 넘겨야 합니다. (US-0174)
        /// 줄 맨 앞의 상태 막대가 여기 듭니다 — 글자가 아니지만 <b>무슨 상태인지를 나릅니다.</b>
        /// </summary>
        [TestCase(true)]
        [TestCase(false)]
        public void 상태를_나르는_색이_보이는_명암비를_넘는다(bool dark)
        {
            AssertReadable("Accent", CsvPalette.Accent(dark), dark, CsvPalette.NonTextContrast);
            AssertReadable("Warning", CsvPalette.Warning(dark), dark, CsvPalette.NonTextContrast);
            AssertReadable("Danger", CsvPalette.Danger(dark), dark, CsvPalette.NonTextContrast);
        }

        /// <summary>
        /// 줄을 가르는 선은 <b>장식</b>이라 3:1 을 요구하지 않습니다. 줄이 갈린다는 사실은
        /// 한 줄 걸러 깔리는 바탕이 이미 나릅니다. 다만 아예 보이지 않으면 그을 이유가 없습니다.
        /// </summary>
        [TestCase(true)]
        [TestCase(false)]
        public void 가름선이_적어도_보이기는_한다(bool dark)
        {
            Color line = CsvPalette.Over(CsvPalette.Divider(dark), CsvPalette.Background(dark));
            float ratio = CsvPalette.ContrastRatio(line, CsvPalette.Background(dark));

            Assert.Greater(ratio, 1.1f,
                           $"{(dark ? "어두운" : "밝은")} 스킨의 가름선이 바탕에 묻힙니다. ({ratio:0.00}:1)");
        }

        /// <summary>
        /// 뜻이 다른 색끼리도 서로 구분돼야 합니다. 바뀜(파랑)·주의(노랑)·오류(빨강)가
        /// 비슷해 보이면 색을 나눈 뜻이 없습니다.
        /// </summary>
        [TestCase(true)]
        [TestCase(false)]
        public void 뜻이_다른_색은_서로_구분된다(bool dark)
        {
            Color accent = CsvPalette.Accent(dark);
            Color warning = CsvPalette.Warning(dark);
            Color danger = CsvPalette.Danger(dark);

            Assert.Greater(Distance(accent, warning), 0.25f, "바뀜과 주의가 너무 비슷합니다.");
            Assert.Greater(Distance(accent, danger), 0.25f, "바뀜과 오류가 너무 비슷합니다.");
            Assert.Greater(Distance(warning, danger), 0.25f, "주의와 오류가 너무 비슷합니다.");
        }

        /// <summary>바탕 두 가지 모두에 대해 요구 명암비를 넘는지 봅니다.</summary>
        /// <param name="name">색의 이름입니다. 실패 안내에 씁니다.</param>
        /// <param name="color">잴 색입니다.</param>
        /// <param name="dark">어두운 스킨인지 여부입니다.</param>
        /// <param name="required">요구되는 최소 명암비입니다.</param>
        private static void AssertReadable(string name, Color color, bool dark, float required)
        {
            float plain = CsvPalette.ContrastRatio(color, CsvPalette.Background(dark));
            float alternate = CsvPalette.ContrastRatio(color, CsvPalette.AlternateBackground(dark));
            float worst = Mathf.Min(plain, alternate);

            Assert.GreaterOrEqual(worst, required,
                $"{(dark ? "어두운" : "밝은")} 스킨의 {name} 가 {worst:0.00}:1 입니다. "
                + $"{required:0.0}:1 을 넘겨야 읽힙니다.");
        }

        /// <summary>두 색이 얼마나 다른지입니다. 채널 차의 크기로 봅니다.</summary>
        /// <param name="a">한쪽 색입니다.</param>
        /// <param name="b">다른 쪽 색입니다.</param>
        /// <returns>거리입니다.</returns>
        private static float Distance(Color a, Color b)
            => Mathf.Sqrt(Mathf.Pow(a.r - b.r, 2) + Mathf.Pow(a.g - b.g, 2) + Mathf.Pow(a.b - b.b, 2));

        // ====================================================================================================
        // 키 — Unity Editor Design System US-0180
        // ====================================================================================================

        /// <summary>목록을 오르내리는 키입니다.</summary>
        [TestCase(KeyCode.UpArrow, CsvListCommand.MoveUp)]
        [TestCase(KeyCode.DownArrow, CsvListCommand.MoveDown)]
        [TestCase(KeyCode.Home, CsvListCommand.MoveFirst)]
        [TestCase(KeyCode.End, CsvListCommand.MoveLast)]
        [TestCase(KeyCode.RightArrow, CsvListCommand.Expand)]
        [TestCase(KeyCode.LeftArrow, CsvListCommand.Collapse)]
        [TestCase(KeyCode.Space, CsvListCommand.Toggle)]
        [TestCase(KeyCode.Return, CsvListCommand.Activate)]
        [TestCase(KeyCode.Escape, CsvListCommand.ClearSearch)]
        public void 키가_명령으로_옮겨진다(KeyCode key, CsvListCommand expected)
            => Assert.AreEqual(expected, CsvListKeys.Read(EventType.KeyDown, key, EventModifiers.None));

        /// <summary>누르는 순간에만 반응합니다. 떼는 것까지 받으면 한 번 누른 것이 두 번이 됩니다.</summary>
        [Test]
        public void 키를_뗄_때는_반응하지_않는다()
            => Assert.AreEqual(CsvListCommand.None,
                               CsvListKeys.Read(EventType.KeyUp, KeyCode.DownArrow, EventModifiers.None));

        /// <summary>찾기는 Ctrl 과 Command 양쪽에서 됩니다.</summary>
        [TestCase(EventModifiers.Control)]
        [TestCase(EventModifiers.Command)]
        public void 찾기는_양쪽_보조키로_된다(EventModifiers modifiers)
            => Assert.AreEqual(CsvListCommand.Find, CsvListKeys.Read(EventType.KeyDown, KeyCode.F, modifiers));

        /// <summary>
        /// 보조 키가 붙은 조합은 목록 명령이 아닙니다. Alt+↓ 같은 것은 다른 뜻으로 예약돼 있어,
        /// 가로채면 사람이 기대한 동작이 사라집니다.
        /// </summary>
        [Test]
        public void 보조키가_붙으면_목록_명령이_아니다()
        {
            Assert.AreEqual(CsvListCommand.None,
                            CsvListKeys.Read(EventType.KeyDown, KeyCode.DownArrow, EventModifiers.Alt));
            Assert.AreEqual(CsvListCommand.None,
                            CsvListKeys.Read(EventType.KeyDown, KeyCode.F, EventModifiers.None));
        }

        /// <summary>목록의 끝에서는 감싸지 않습니다. 감싸면 어디까지 왔는지 알 수 없습니다.</summary>
        [Test]
        public void 목록_끝에서는_감싸지_않는다()
        {
            Assert.AreEqual(0, CsvListKeys.Move(CsvListCommand.MoveUp, 0, 3));
            Assert.AreEqual(2, CsvListKeys.Move(CsvListCommand.MoveDown, 2, 3));
        }

        /// <summary>고른 것이 없으면 어느 쪽 키를 눌러도 첫 항목이 잡힙니다.</summary>
        [Test]
        public void 고른_것이_없으면_첫_항목을_잡는다()
        {
            Assert.AreEqual(0, CsvListKeys.Move(CsvListCommand.MoveDown, -1, 3));
            Assert.AreEqual(0, CsvListKeys.Move(CsvListCommand.MoveUp, -1, 3));
        }

        /// <summary>목록이 비어 있으면 고를 자리가 없습니다.</summary>
        [Test]
        public void 빈_목록에서는_고를_자리가_없다()
            => Assert.AreEqual(-1, CsvListKeys.Move(CsvListCommand.MoveDown, -1, 0));

        /// <summary>목록이 줄어들면 골라 둔 자리가 범위 안으로 당겨집니다.</summary>
        [Test]
        public void 목록이_줄면_고른_자리가_당겨진다()
            => Assert.AreEqual(1, CsvListKeys.Move(CsvListCommand.None, 9, 2));

        // ====================================================================================================
        // 거르기
        // ====================================================================================================

        /// <summary>바뀌는 것이 없는 표를 만듭니다.</summary>
        /// <returns>계획입니다.</returns>
        private static CsvImportPlan QuietPlan()
            => new CsvImportPlan("Quests.csv", "QuestData") { OutputFolder = "Assets/Data/Quests" };

        /// <summary>바뀌는 것이 있는 표를 만듭니다.</summary>
        /// <returns>계획입니다.</returns>
        private static CsvImportPlan ChangedPlan()
        {
            CsvImportPlan plan = QuietPlan();
            plan.Add(CsvChangeKind.Create, "Assets/Data/Quests/Q_01.asset");
            return plan;
        }

        /// <summary>문제가 있는 표를 만듭니다.</summary>
        /// <returns>계획입니다.</returns>
        private static CsvImportPlan ProblemPlan()
        {
            CsvImportPlan plan = QuietPlan();
            plan.Issues.Add(new CsvIssue(CsvIssueSeverity.Warning, "겹친 식별자가 있습니다."));
            return plan;
        }

        /// <summary>'바뀌는 것만' 은 표와 같은 산출물을 감춥니다.</summary>
        [Test]
        public void 바뀌는_것만_보기가_같은_표를_감춘다()
        {
            Assert.IsFalse(CsvTableFilter.MatchesView(QuietPlan(), CsvTableView.Changed));
            Assert.IsTrue(CsvTableFilter.MatchesView(ChangedPlan(), CsvTableView.Changed));
            Assert.IsTrue(CsvTableFilter.MatchesView(ProblemPlan(), CsvTableView.Changed));
        }

        /// <summary>'손볼 것만' 은 문제가 있는 표만 남깁니다. 그냥 바뀌는 표는 감춥니다.</summary>
        [Test]
        public void 손볼_것만_보기가_문제만_남긴다()
        {
            Assert.IsFalse(CsvTableFilter.MatchesView(QuietPlan(), CsvTableView.Problems));
            Assert.IsFalse(CsvTableFilter.MatchesView(ChangedPlan(), CsvTableView.Problems));
            Assert.IsTrue(CsvTableFilter.MatchesView(ProblemPlan(), CsvTableView.Problems));
        }

        /// <summary>'전부' 는 아무것도 감추지 않습니다.</summary>
        [Test]
        public void 전부_보기는_아무것도_감추지_않는다()
            => Assert.IsTrue(CsvTableFilter.MatchesView(QuietPlan(), CsvTableView.All));

        /// <summary>검색어는 표 이름·타입 이름·산출물 폴더에 걸립니다.</summary>
        [TestCase("quests")]
        [TestCase("QUESTDATA")]
        [TestCase("Assets/Data")]
        public void 검색어가_이름과_폴더에_걸린다(string search)
            => Assert.IsTrue(CsvTableFilter.MatchesSearch(QuietPlan(), search));

        /// <summary>걸리지 않는 검색어는 걸러 냅니다.</summary>
        [Test]
        public void 걸리지_않는_검색어는_거른다()
            => Assert.IsFalse(CsvTableFilter.MatchesSearch(QuietPlan(), "존재하지않는이름"));

        /// <summary>빈 검색어는 아무것도 거르지 않습니다. 공백만 친 것도 같습니다.</summary>
        [Test]
        public void 빈_검색어는_거르지_않는다()
        {
            Assert.IsTrue(CsvTableFilter.MatchesSearch(QuietPlan(), string.Empty));
            Assert.IsTrue(CsvTableFilter.MatchesSearch(QuietPlan(), "   "));
        }

        /// <summary>보기를 돌리면 셋을 한 바퀴 돌아 제자리로 옵니다.</summary>
        [Test]
        public void 보기는_한_바퀴_돈다()
        {
            CsvTableView view = CsvTableView.Changed;
            for (int i = 0; i < 3; i++) view = CsvTableFilter.Next(view);

            Assert.AreEqual(CsvTableView.Changed, view);
        }

        /// <summary>보기마다 이름과 설명이 있습니다. 무엇이 감춰졌는지 말할 수 있어야 합니다.</summary>
        [TestCase(CsvTableView.Changed)]
        [TestCase(CsvTableView.Problems)]
        [TestCase(CsvTableView.All)]
        public void 보기마다_이름과_설명이_있다(CsvTableView view)
        {
            Assert.IsNotEmpty(CsvTableFilter.Label(view));
            Assert.IsNotEmpty(CsvTableFilter.Describe(view));
        }
    }
}
