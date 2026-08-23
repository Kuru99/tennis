namespace PrideCourt.Presentation
{
    public enum FrontEndScreen
    {
        Opening,
        Title,
        ModeSelect,
        MultiplayerSelect,
        LocalNetworkLobby,
        OnlineNetworkLobby,
        Setup
    }

    public enum FrontEndAction
    {
        FinishOpening,
        ReplayOpening,
        OpenModeSelect,
        SelectSolo,
        SelectMultiplayer,
        SelectLocal,
        SelectNetwork,
        Back,
        ReturnToSetup,
        ReturnToLocalLobby,
        ReturnToOnlineLobby,
        ReturnToTitle
    }

    public enum MultiplayerEntry
    {
        None,
        Local,
        Network
    }

    /// <summary>
    /// Owns only front-end navigation rules. Rendering and match startup remain outside this class.
    /// </summary>
    public sealed class PrideCourtFrontEndFlow
    {
        public const bool NetworkBattleAvailable = false;

        public FrontEndScreen Screen { get; private set; } = FrontEndScreen.Opening;
        public MultiplayerEntry PendingMultiplayerEntry { get; private set; }

        public bool Apply(FrontEndAction action)
        {
            switch (action)
            {
                case FrontEndAction.FinishOpening when Screen == FrontEndScreen.Opening:
                    Screen = FrontEndScreen.Title;
                    return true;

                case FrontEndAction.ReplayOpening when Screen == FrontEndScreen.Title:
                    Screen = FrontEndScreen.Opening;
                    return true;

                case FrontEndAction.OpenModeSelect when Screen == FrontEndScreen.Title:
                    Screen = FrontEndScreen.ModeSelect;
                    return true;

                case FrontEndAction.SelectSolo when Screen == FrontEndScreen.ModeSelect:
                    Screen = FrontEndScreen.Setup;
                    return true;

                case FrontEndAction.SelectMultiplayer when Screen == FrontEndScreen.ModeSelect:
                    Screen = FrontEndScreen.MultiplayerSelect;
                    return true;

                case FrontEndAction.SelectLocal when Screen == FrontEndScreen.MultiplayerSelect:
                    PendingMultiplayerEntry = MultiplayerEntry.Local;
                    Screen = FrontEndScreen.LocalNetworkLobby;
                    return true;

                case FrontEndAction.SelectNetwork when Screen == FrontEndScreen.MultiplayerSelect &&
                                                           NetworkBattleAvailable:
                    PendingMultiplayerEntry = MultiplayerEntry.Network;
                    Screen = FrontEndScreen.OnlineNetworkLobby;
                    return true;

                case FrontEndAction.ReturnToSetup:
                    PendingMultiplayerEntry = MultiplayerEntry.None;
                    Screen = FrontEndScreen.Setup;
                    return true;

                case FrontEndAction.ReturnToLocalLobby:
                    PendingMultiplayerEntry = MultiplayerEntry.Local;
                    Screen = FrontEndScreen.LocalNetworkLobby;
                    return true;

                case FrontEndAction.ReturnToOnlineLobby:
                    PendingMultiplayerEntry = MultiplayerEntry.Network;
                    Screen = FrontEndScreen.OnlineNetworkLobby;
                    return true;

                case FrontEndAction.ReturnToTitle:
                    PendingMultiplayerEntry = MultiplayerEntry.None;
                    Screen = FrontEndScreen.Title;
                    return true;

                case FrontEndAction.Back:
                    return GoBack();

                default:
                    return false;
            }
        }

        private bool GoBack()
        {
            switch (Screen)
            {
                case FrontEndScreen.LocalNetworkLobby:
                case FrontEndScreen.OnlineNetworkLobby:
                    PendingMultiplayerEntry = MultiplayerEntry.None;
                    Screen = FrontEndScreen.MultiplayerSelect;
                    return true;
                case FrontEndScreen.MultiplayerSelect:
                case FrontEndScreen.Setup:
                    Screen = FrontEndScreen.ModeSelect;
                    return true;
                case FrontEndScreen.ModeSelect:
                    Screen = FrontEndScreen.Title;
                    return true;
                default:
                    return false;
            }
        }
    }
}
