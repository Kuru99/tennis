namespace PrideCourt.Presentation
{
    public enum SetupStep
    {
        CharacterSelect,
        CardLoadout
    }

    public enum SetupAction
    {
        ConfirmCharacter,
        Back
    }

    /// <summary>
    /// Owns only the pre-match character/loadout navigation rules.
    /// Rendering, deck rules, and match startup remain outside this class.
    /// </summary>
    public sealed class PrideCourtSetupFlow
    {
        public SetupStep Step { get; private set; } = SetupStep.CharacterSelect;

        public bool Apply(SetupAction action)
        {
            switch (action)
            {
                case SetupAction.ConfirmCharacter when Step == SetupStep.CharacterSelect:
                    Step = SetupStep.CardLoadout;
                    return true;

                case SetupAction.Back when Step == SetupStep.CardLoadout:
                    Step = SetupStep.CharacterSelect;
                    return true;

                default:
                    return false;
            }
        }

        public void Reset()
        {
            Step = SetupStep.CharacterSelect;
        }
    }
}
