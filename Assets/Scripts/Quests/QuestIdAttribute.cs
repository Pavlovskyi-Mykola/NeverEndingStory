using UnityEngine;

/// <summary>
/// Marks a string field as a quest id. Drawn with a picker over the QuestId of
/// every QuestDefinition asset, highlighted when the id matches no known quest.
/// Also works on List&lt;string&gt;/string[] fields (applied per element).
/// </summary>
public class QuestIdAttribute : PropertyAttribute
{
}
