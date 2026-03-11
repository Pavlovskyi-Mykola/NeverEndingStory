public enum QuestStepType
{
    AutoComplete = 0,
    Manual = 1,

    TalkToNpc = 10,        // later
    ReachLocation = 11,    // Step 2
    HaveItem = 12,         // later
    MinStats = 13,         // Step 2
    HaveMoney = 14,        // Step 2 (money requirement)
    PayMoney = 15,         // Step 2 (payment)
    TimeWindow = 16,       // later (we'll do simple day/phase constraint now)
}