using NUnit.Framework;

namespace CsvPipeline.Tests
{
    /// <summary>
    /// 설정 조회가 <b>프로젝트를 몇 번 뒤지는지</b>를 봅니다.
    ///
    /// 이 검사는 실제로 에디터를 죽인 결함 때문에 생겼습니다.
    /// <c>ExistsInProject</c> 가 부를 때마다 프로젝트 전체 검색을 돌았고,
    /// 그 값을 그리기에서 읽는 화면이 셋이었습니다. 설정 화면에 마우스를 올려 둔 것만으로
    /// 메모리가 계속 늘어 <c>Could not allocate memory: System out of memory!</c> 로 끝났습니다.
    /// 값이 맞는지만 보는 검사로는 잡히지 않아, <b>몇 번 부르는지</b>를 셉니다.
    /// </summary>
    public sealed class CsvPipelineSettingsTests
    {
        private MemoryAssetGateway _assets;
        private System.IDisposable _scope;

        /// <summary>메모리 게이트웨이를 끼우고 들고 있던 설정을 버립니다.</summary>
        [SetUp]
        public void SetUp()
        {
            _assets = new MemoryAssetGateway();
            _scope = CsvAssets.Use(_assets);
            CsvPipelineSettings.InvalidateCache();
        }

        /// <summary>게이트웨이를 걷어내고 캐시를 비웁니다. 다음 검사에 새어 나가면 안 됩니다.</summary>
        [TearDown]
        public void TearDown()
        {
            CsvPipelineSettings.InvalidateCache();
            _scope.Dispose();
            _assets.Dispose();
            CsvPipelineSettings.InvalidateCache();
        }

        // ====================================================================================================

        /// <summary>
        /// <b>이 검사가 이 파일의 이유입니다.</b>
        /// 그리기는 마우스가 움직이는 동안에도 계속 도는데, 그때마다 프로젝트를 뒤지면 안 됩니다.
        /// </summary>
        [Test]
        public void 여러_번_물어도_프로젝트는_한_번만_뒤진다()
        {
            _assets.Add<CsvPipelineSettings>("Assets/CsvPipelineSettings.asset");
            _assets.ResetCounters();

            for (int i = 0; i < 200; i++)
            {
                bool _ = CsvPipelineSettings.ExistsInProject;
            }

            Assert.AreEqual(1, _assets.FindPathsCount,
                            "그리기에서 읽는 값이라 부를 때마다 프로젝트를 뒤지면 메모리가 계속 늘어납니다.");
        }

        /// <summary>설정 에셋이 없을 때도 반복 조회가 프로젝트를 다시 뒤지지 않습니다.</summary>
        [Test]
        public void 설정이_없어도_한_번만_뒤진다()
        {
            _assets.ResetCounters();

            for (int i = 0; i < 200; i++)
            {
                bool _ = CsvPipelineSettings.ExistsInProject;
            }

            Assert.AreEqual(1, _assets.FindPathsCount);
            Assert.IsFalse(CsvPipelineSettings.ExistsInProject);
        }

        /// <summary><c>Instance</c> 와 <c>ExistsInProject</c> 를 섞어 물어도 한 번뿐입니다.</summary>
        [Test]
        public void 인스턴스와_존재여부가_같은_조회를_쓴다()
        {
            _assets.Add<CsvPipelineSettings>("Assets/CsvPipelineSettings.asset");
            _assets.ResetCounters();

            for (int i = 0; i < 50; i++)
            {
                CsvPipelineSettings settings = CsvPipelineSettings.Instance;
                bool exists = CsvPipelineSettings.ExistsInProject;

                Assert.IsNotNull(settings);
                Assert.IsTrue(exists);
            }

            Assert.AreEqual(1, _assets.FindPathsCount);
        }

        /// <summary>설정 에셋이 있으면 있다고 답합니다.</summary>
        [Test]
        public void 설정이_있으면_있다고_답한다()
        {
            Assert.IsFalse(CsvPipelineSettings.ExistsInProject);

            _assets.Add<CsvPipelineSettings>("Assets/CsvPipelineSettings.asset");
            CsvPipelineSettings.InvalidateCache();

            Assert.IsTrue(CsvPipelineSettings.ExistsInProject);
        }

        /// <summary>
        /// 캐시를 버리면 다시 뒤집니다. 버리는 길이 막혀 있으면 지운 설정을 계속 있다고 말하게 됩니다.
        /// (실제로 <c>InvalidateCache</c> 는 만들어져 있었지만 <b>부르는 곳이 하나도 없었습니다.</b>)
        /// </summary>
        [Test]
        public void 캐시를_버리면_다시_뒤진다()
        {
            _assets.Add<CsvPipelineSettings>("Assets/CsvPipelineSettings.asset");
            CsvPipelineSettings.InvalidateCache();

            Assert.IsTrue(CsvPipelineSettings.ExistsInProject);

            _assets.Delete("Assets/CsvPipelineSettings.asset");
            CsvPipelineSettings.InvalidateCache();

            Assert.IsFalse(CsvPipelineSettings.ExistsInProject,
                           "지운 설정을 계속 있다고 말하면 화면이 틀린 사실을 보여 줍니다.");
        }

        /// <summary>설정 에셋이 없어도 기본값 인스턴스를 돌려줍니다. null 이면 안 됩니다.</summary>
        [Test]
        public void 설정이_없어도_기본값을_돌려준다()
        {
            CsvPipelineSettings settings = CsvPipelineSettings.Instance;

            Assert.IsNotNull(settings);
            Assert.IsFalse(string.IsNullOrEmpty(settings.CsvRootFolder));
        }
    }
}
