namespace game_1;

public enum GameState
{
    // Title screen: waiting for the player to start a run.
    Start,

    // A run is in progress and mobs are spawning.
    Playing,

    // A run on hold: everything frozen where it stood, with the choice to pick
    // it back up or save it and quit.
    Paused,

    // The player ran out of touches; showing their time until they play
    // again or quit.
    GameOver
}
