namespace LegalAssistant.Workers
{
    public partial class IngestWorker
    {
        private class IngestPayload { public string DocumentId { get; set; } public string Url { get; set; } }
    }
}
