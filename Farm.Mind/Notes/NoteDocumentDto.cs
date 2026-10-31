namespace Farm.Mind.Notes;

public record NoteDocumentDto(string WorkspaceId, string DocId, string Markdown, int? NextOffset);
