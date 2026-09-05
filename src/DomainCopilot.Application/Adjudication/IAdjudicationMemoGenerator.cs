namespace DomainCopilot.Application.Adjudication;

/// <summary>Port over T6's document-out half: rendering a case's grounded data into a
/// professionally formatted PDF. Infrastructure provides the only implementation (QuestPDF);
/// Application never references a document-generation SDK directly.</summary>
public interface IAdjudicationMemoGenerator
{
    byte[] Generate(AdjudicationMemoData data);
}
