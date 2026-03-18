namespace LegalAssistant.Domain.Documents;

public interface IHtmlToTextConverter
{
    string Convert(string html);
}
