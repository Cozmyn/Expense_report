using Decont;using Xunit;
namespace Decont.Tests;
public class OcrHealthTests {
 [Fact]public void MissingTrainedModelsCannotBeReportedAsReady(){Assert.Equal(new[]{"ron"},LocalDocumentExtractor.MissingModels("ron+eng","List of available languages in /usr/share/tessdata/ (1):\neng\n"));Assert.Empty(LocalDocumentExtractor.MissingModels("ron+eng","List of available languages (2):\neng\nron\n"));Assert.Equal(new[]{"ron","eng"},LocalDocumentExtractor.MissingModels("ron+eng",null));}
 [Fact]public void LanguageNamesAreMatchedExactly(){Assert.Equal(new[]{"ron"},LocalDocumentExtractor.MissingModels("ron+eng","ron_extra\neng"));Assert.Empty(LocalDocumentExtractor.MissingModels(" eng + ron ","ron\r\neng\r\n"));}
}
