public enum BattleState
{
    Initializing,
    PlayerTurn_AwaitingInput,
    PlayerTurn_SelectingSkill,
    PlayerTurn_Gesture,
    ActionInProgress,
    MonsterTurn,
    BattleOver_Victory,
    BattleOver_Defeat,
    Escaped
}

public enum BattleEventType
{
    BattleStarted,
    BattleVictory,
    BattleDefeat,
    BattleEscaped,
    PlayerTurnStarted,
    MonsterTurnStarted,
    PlayerAttackRequested,
    PlayerSkillSelected,
    PlayerAttemptEscape,
    GestureRecognitionStarted,
    PlayerAttackSuccess,
    PlayerAttackFail,
    MonsterAttack,
    TutorialStart
}