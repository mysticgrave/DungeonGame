namespace DungeonGame.Interaction
{
    /// <summary>
    /// Implement on any MonoBehaviour/NetworkBehaviour that a player can interact with
    /// using the F key (via PlayerInteractor). Examples: dungeon board, torches, NPCs, chests.
    /// </summary>
    public interface IInteractable
    {
        /// <summary>Text shown on screen when the player looks at this object, e.g. "Enter Dungeon".</summary>
        string InteractPrompt { get; }

        /// <summary>
        /// Server-side check: can this client interact right now?
        /// Return false to hide the prompt (e.g. host-only actions, already used, etc.).
        /// </summary>
        bool CanInteract(ulong clientId);

        /// <summary>
        /// Execute the interaction. Called on the server after CanInteract returns true.
        /// </summary>
        void Interact(ulong clientId);
    }
}
