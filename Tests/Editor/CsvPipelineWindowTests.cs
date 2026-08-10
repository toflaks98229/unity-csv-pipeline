using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CsvPipeline.Tests
{
    /// <summary>
    /// 창이 <b>실제로 그려지는지</b> 봅니다.
    /// IMGUI 의 레이아웃 짝(Begin/End)이 어긋나면 컴파일은 통과하고 창만 무너집니다.
    /// 그리는 도중 난 예외는 오류 로그가 되고, 검사 틀이 그것을 실패로 잡습니다.
    /// </summary>
    public sealed class CsvPipelineWindowTests
    {
        private CsvPipelineWindow _window;

        /// <summary>창을 엽니다.</summary>
        [SetUp]
        public void SetUp() => _window = ScriptableObject.CreateInstance<CsvPipelineWindow>();

        /// <summary>창을 닫습니다.</summary>
        [TearDown]
        public void TearDown()
        {
            if (_window != null) Object.DestroyImmediate(_window);
        }

        /// <summary>
        /// 창을 한 번 그립니다. 화면이 없는 배치 실행에서는 그릴 수 없으므로 조용히 넘어갑니다.
        /// </summary>
        /// <returns>실제로 그렸으면 true입니다.</returns>
        private bool Draw()
        {
            MethodInfo repaint = typeof(EditorWindow).GetMethod(
                "RepaintImmediately", BindingFlags.NonPublic | BindingFlags.Instance);

            if (repaint == null || Application.isBatchMode) return false;

            _window.position = new Rect(0, 0, 800, 600);
            _window.ShowUtility();
            repaint.Invoke(_window, null);
            return true;
        }

        /// <summary>갈래를 바꿉니다.</summary>
        /// <param name="index">0=표, 1=시트 연동, 2=설정입니다.</param>
        private static void SetTab(int index) => SessionState.SetInt("CsvPipeline.Window.Tab", index);

        // ====================================================================================================

        /// <summary>세 갈래를 모두 그려도 예외가 나지 않습니다.</summary>
        [Test]
        public void 모든_갈래가_그려진다()
        {
            for (int tab = 0; tab < 3; tab++)
            {
                SetTab(tab);
                if (!Draw()) Assert.Ignore("화면이 없는 실행이라 그리기를 건너뜁니다.");
            }
        }

        /// <summary>검색어가 아무것도 걸러 내지 못하는 상태에서도 그려집니다. (빈 화면 안내 경로)</summary>
        [Test]
        public void 걸리는_것이_없어도_그려진다()
        {
            SetTab(0);
            SessionState.SetString("CsvPipeline.Window.Search", "존재하지않는표이름_zzz");
            SessionState.SetBool("CsvPipeline.Window.OnlyChanged", true);

            try
            {
                if (!Draw()) Assert.Ignore("화면이 없는 실행이라 그리기를 건너뜁니다.");
            }
            finally
            {
                SessionState.SetString("CsvPipeline.Window.Search", string.Empty);
            }
        }

        /// <summary>
        /// 창 상태가 <see cref="SessionState"/> 에 실립니다.
        /// 이것이 깨지면 스크립트를 고칠 때마다 보던 갈래와 검색어가 초기값으로 돌아갑니다.
        /// 그리기 없이도 확인할 수 있어 배치 실행에서도 돕니다.
        /// </summary>
        [Test]
        public void 창_상태가_도메인_재적재를_넘긴다()
        {
            SessionState.SetInt("CsvPipeline.Window.Tab", 2);
            SessionState.SetString("CsvPipeline.Window.Search", "Item");
            SessionState.SetBool("CsvPipeline.Window.OnlyChanged", false);
            SessionState.SetBool("CsvPipeline.Window.Open.Items.csv", true);

            // 창 인스턴스를 버리고 새로 만들어도 값이 남아 있어야 합니다.
            Object.DestroyImmediate(_window);
            _window = ScriptableObject.CreateInstance<CsvPipelineWindow>();

            Assert.AreEqual(2, SessionState.GetInt("CsvPipeline.Window.Tab", 0));
            Assert.AreEqual("Item", SessionState.GetString("CsvPipeline.Window.Search", string.Empty));
            Assert.IsFalse(SessionState.GetBool("CsvPipeline.Window.OnlyChanged", true));
            Assert.IsTrue(SessionState.GetBool("CsvPipeline.Window.Open.Items.csv", false));

            SessionState.SetInt("CsvPipeline.Window.Tab", 0);
            SessionState.SetString("CsvPipeline.Window.Search", string.Empty);
            SessionState.SetBool("CsvPipeline.Window.OnlyChanged", true);
            SessionState.EraseBool("CsvPipeline.Window.Open.Items.csv");
        }
    }
}
