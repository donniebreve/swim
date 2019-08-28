using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Common.Configuration;
using Common.Migration;
using Common.Configuration.Json;

namespace UnitTests.Migration.Phase2.Processors
{
    [TestClass]
    public class TargetPostMoveTagsProcessorTests
    {
        private Mock<IMigrationContext> MigrationContextMock;

        [TestInitialize]
        public void Initialize()
        {
            this.MigrationContextMock = new Mock<IMigrationContext>();
        }

        [TestMethod]
        public void GetUpdatedTagsFieldWithPostMove_ReturnsCorrectValue()
        {
            IConfiguration configuration = new Configuration();
            configuration.TargetPostMoveTag = "sample-post-move-tag";
            this.MigrationContextMock.SetupGet(a => a.Configuration).Returns(configuration);
            string tagFieldValue = "originalTag";
            string expected = "originalTag; sample-post-move-tag";

            TargetPostMoveTagsProcessor targetPostMoveTagsProcessor = new TargetPostMoveTagsProcessor();
            string actual = targetPostMoveTagsProcessor.GetUpdatedTagsFieldWithPostMove(this.MigrationContextMock.Object, tagFieldValue);

            Assert.AreEqual(expected, actual);
        }
    }
}
