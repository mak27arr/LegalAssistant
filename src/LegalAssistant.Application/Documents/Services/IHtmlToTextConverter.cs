namespace LegalAssistant.Application.Documents.Services;

public interface IHtmlToTextConverter
{
    string Convert(string html);
}
