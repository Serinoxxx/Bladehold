/// <summary>
///     Run-scoped state that must survive a scene reload within the same session. <see cref="DeathScreen" />
///     reloads the scene to restart; scene singletons (<see cref="GameStats" />, <see cref="Wallet" />)
///     reset naturally, but the wave to resume from must persist across that reload, so it lives here as a
///     static rather than on a scene object.
///
///     Statics persist across <c>SceneManager.LoadScene</c> but reset when play stops, so this is
///     deliberately session-only ("restart from the wave I died on"), not saved to disk.
/// </summary>
public static class RunState
{
    /// <summary>
    ///     The wave <see cref="WaveSpawner" /> begins from on load. Set to 1 to restart from the start,
    ///     or to the current wave to resume after death.
    /// </summary>
    public static int StartingWave = 1;
}
