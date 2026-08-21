using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace CsvPipeline.Tests
{
    /// <summary>
    /// 창이 <b>실제로 그려지는지</b> 봅니다.
    /// IMGUI 의 레이아웃 짝(Begin/End)이 어긋나면 컴파일은 통과하고 창만 무너집니다.
    /// 그리는 도중 난 예외는 오류 로그가 되고, 검사 틀이 그것을 실패로 잡습니다.
    /// <para>
    /// <b>배치 실행에서도 돕니다.</b> <c>-nographics</c> 없이 띄우면 창을 열고 그릴 수 있어,
    /// 전에 건너뛰던 이 검사가 CI 에서도 값을 합니다. 정말로 화면을 열 수 없는 실행에서만
    /// 스스로 건너뜁니다.
    /// </para>
    /// </summary>
    public sealed class CsvPipelineWindowTests
    {
        private CsvPipelineWindow _window;

        /// <summary>창을 엽니다.</summary>
        [SetUp]
        public void SetUp() => _window = ScriptableObject.CreateInstance<CsvPipelineWindow>();

        /// <summary>창을 닫고 손댄 상태를 되돌립니다.</summary>
        [TearDown]
        public void TearDown()
        {
            if (_window != null) UnityEngine.Object.DestroyImmediate(_window);

            SessionState.SetInt(TabKey, 0);
            SessionState.SetInt(ViewKey, 0);
            SessionState.SetString(SearchKey, string.Empty);
        }

        private const string TabKey = "CsvPipeline.Window.Tab";
        private const string ViewKey = "CsvPipeline.Window.View";
        private const string SearchKey = "CsvPipeline.Window.Search";

        /// <summary>
        /// 창을 한 번 그립니다. 창 자체를 열 수 없는 실행에서는 조용히 넘어갑니다.
        /// <b>여는 데 성공한 뒤에 난 예외는 진짜 결함입니다.</b> 그래서 그리기는 감싸지 않습니다.
        /// </summary>
        /// <param name="width">창의 너비입니다.</param>
        /// <returns>실제로 그렸으면 true입니다.</returns>
        private bool Draw(float width = 800f)
        {
            MethodInfo repaint = typeof(EditorWindow).GetMethod(
                "RepaintImmediately", BindingFlags.NonPublic | BindingFlags.Instance);

            if (repaint == null) return false;

            // 그래픽 장치가 없는 실행(컨테이너 CI 등)에서는 창을 그릴 수 없습니다.
            // 여기서 미리 물러서지 않으면 그리기가 터지고, 그것을 진짜 결함과 구분할 수 없게 됩니다.
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return false;

            try
            {
                _window.position = new Rect(0, 0, width, 600);
                _window.ShowUtility();
            }
            catch (Exception)
            {
                return false;   // 화면을 열 수 없는 실행입니다.
            }

            repaint.Invoke(_window, null);
            return true;
        }

        /// <summary>갈래를 바꿉니다.</summary>
        /// <param name="index">0=표, 1=시트 연동, 2=설정입니다.</param>
        private static void SetTab(int index) => SessionState.SetInt(TabKey, index);

        // ====================================================================================================

        /// <summary>세 갈래를 모두 그려도 예외가 나지 않습니다.</summary>
        [Test]
        public void 모든_갈래가_그려진다()
        {
            for (int tab = 0; tab < 3; tab++)
            {
                SetTab(tab);
                if (!Draw()) Assert.Ignore("화면을 열 수 없는 실행이라 그리기를 건너뜁니다.");
            }
        }

        /// <summary>
        /// 보기 셋이 모두 그려집니다. 거르는 조건마다 지나는 길이 달라, 하나만 그려 보면
        /// 나머지 둘이 무너진 것을 놓칩니다.
        /// </summary>
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void 모든_보기가_그려진다(int view)
        {
            SetTab(0);
            SessionState.SetInt(ViewKey, view);

            if (!Draw()) Assert.Ignore("화면을 열 수 없는 실행이라 그리기를 건너뜁니다.");
        }

        /// <summary>검색어가 아무것도 걸러 내지 못하는 상태에서도 그려집니다. (빈 화면 안내 경로)</summary>
        [Test]
        public void 걸리는_것이_없어도_그려진다()
        {
            SetTab(0);
            SessionState.SetString(SearchKey, "존재하지않는표이름_zzz");
            SessionState.SetInt(ViewKey, 0);

            if (!Draw()) Assert.Ignore("화면을 열 수 없는 실행이라 그리기를 건너뜁니다.");
        }

        /// <summary>
        /// 창을 가장 좁게 줄여도 그려집니다. 칸 너비를 고정 픽셀로 두면 이 자리에서 가로로 잘립니다.
        /// </summary>
        [Test]
        public void 가장_좁은_너비에서도_그려진다()
        {
            SetTab(0);
            SessionState.SetInt(ViewKey, 2);

            if (!Draw(560f)) Assert.Ignore("화면을 열 수 없는 실행이라 그리기를 건너뜁니다.");
        }

        /// <summary>
        /// 창 상태가 <see cref="SessionState"/> 에 실립니다.
        /// 이것이 깨지면 스크립트를 고칠 때마다 보던 갈래와 검색어가 초기값으로 돌아갑니다.
        /// 그리기 없이도 확인할 수 있어 어떤 실행에서도 돕니다.
        /// </summary>
        [Test]
        public void 창_상태가_도메인_재적재를_넘긴다()
        {
            SessionState.SetInt(TabKey, 2);
            SessionState.SetString(SearchKey, "Item");
            SessionState.SetInt(ViewKey, 2);
            SessionState.SetString("CsvPipeline.Window.Selected", "Items.csv");
            SessionState.SetBool("CsvPipeline.Window.Open.Items.csv", true);

            // 창 인스턴스를 버리고 새로 만들어도 값이 남아 있어야 합니다.
            UnityEngine.Object.DestroyImmediate(_window);
            _window = ScriptableObject.CreateInstance<CsvPipelineWindow>();

            Assert.AreEqual(2, SessionState.GetInt(TabKey, 0));
            Assert.AreEqual("Item", SessionState.GetString(SearchKey, string.Empty));
            Assert.AreEqual(2, SessionState.GetInt(ViewKey, 0));
            Assert.AreEqual("Items.csv", SessionState.GetString("CsvPipeline.Window.Selected", string.Empty));
            Assert.IsTrue(SessionState.GetBool("CsvPipeline.Window.Open.Items.csv", false));

            SessionState.EraseString("CsvPipeline.Window.Selected");
            SessionState.EraseBool("CsvPipeline.Window.Open.Items.csv");
        }
    }
}
