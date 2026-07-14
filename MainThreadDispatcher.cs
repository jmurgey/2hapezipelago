using System;
using System.Collections.Concurrent;
using MonoMod.RuntimeDetour;
using ShapezShifter.Flow;
using ShapezShifter.Hijack;

namespace _2hapezipelago
{
    /// <summary>
    /// Runs queued actions on the game's main thread.
    ///
    /// Archipelago's <c>ItemReceived</c> callback fires on the MultiClient.Net background
    /// socket thread. Touching Unity / game state from that thread crashes the game, so any
    /// work that mutates the game (granting rewards, unlocking research, HUD refreshes) must
    /// be marshalled here and drained during <see cref="GameSessionOrchestrator.Tick"/>,
    /// which runs on the main thread.
    /// </summary>
    public class MainThreadDispatcher : IDisposable
    {
        private readonly ConcurrentQueue<Action> _queue = new ConcurrentQueue<Action>();
        private APMod? _mod;
        private readonly Hook _tickHook;

        public MainThreadDispatcher(APMod mod)
        {
            _mod = mod;
            _tickHook = ShapezShifter.SharpDetour.DetourHelper.CreatePostfixHook<GameSessionOrchestrator, float>(
                (orch, time) => orch.Tick(time),
                (orch, time) => Drain());
        }

        /// <summary>Queue an action to run on the next main-thread tick. Safe to call from any thread.</summary>
        public void Enqueue(Action action)
        {
            _queue.Enqueue(action);
        }

        private void Drain()
        {
            while (_queue.TryDequeue(out var action))
            {
                try
                {
                    action();
                }
                catch (Exception e)
                {
                    _mod?.Logger.Warning?.Log("Queued main-thread action failed: " + e.Message);
                }
            }
        }

        public void Dispose()
        {
            _tickHook.Dispose();
            _mod = null;
        }
    }
}
