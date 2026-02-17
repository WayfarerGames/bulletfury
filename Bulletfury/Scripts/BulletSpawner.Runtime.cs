using Common;
using Common.FloatOrRandom;

namespace BulletFury
{
    public interface ISpawnerRuntimeModule
    {
        public float LastSimulationDeltaTime { get; }
        public void OnRuntimeReset(Squirrel3 random);
        public void OnSimulationStep(float deltaTime);
        public float Sample(FloatOrRandom value, Squirrel3 random);
        public object CaptureState();
        public void RestoreState(object state);
    }

    public interface ISpawnerRuntimeModuleProvider : IBaseBulletModule
    {
        public ISpawnerRuntimeModule CreateRuntimeModule();
    }

    internal sealed class BulletSpawnerRuntime
    {
        private sealed class DefaultSpawnerRuntimeModule : ISpawnerRuntimeModule
        {
            public float LastSimulationDeltaTime { get; private set; }

            public void OnRuntimeReset(Squirrel3 random)
            {
                LastSimulationDeltaTime = 0f;
            }

            public void OnSimulationStep(float deltaTime)
            {
                LastSimulationDeltaTime = deltaTime;
            }

            public float Sample(FloatOrRandom value, Squirrel3 random)
            {
                return value.Value;
            }

            public object CaptureState()
            {
                return LastSimulationDeltaTime;
            }

            public void RestoreState(object state)
            {
                LastSimulationDeltaTime = state is float lastDeltaTime ? lastDeltaTime : 0f;
            }
        }

        private struct RuntimeState
        {
            public Squirrel3.State RandomState;
            public object ModuleState;
        }

        private static readonly ISpawnerRuntimeModule DefaultRuntimeModule = new DefaultSpawnerRuntimeModule();
        private ISpawnerRuntimeModule _runtimeModule = DefaultRuntimeModule;
        private readonly Squirrel3 _random = new();

        public float LastSimulationDeltaTime => _runtimeModule.LastSimulationDeltaTime;
        public Squirrel3 Random => _random;

        public void SetRuntimeModule(ISpawnerRuntimeModule runtimeModule)
        {
            _runtimeModule = runtimeModule ?? DefaultRuntimeModule;
        }

        public void ResetRuntimeState()
        {
            _runtimeModule.OnRuntimeReset(_random);
        }

        public void AdvanceSimulation(float deltaTime)
        {
            _runtimeModule.OnSimulationStep(deltaTime);
        }

        public float Sample(FloatOrRandom value)
        {
            return _runtimeModule.Sample(value, _random);
        }

        public object CaptureState()
        {
            return new RuntimeState
            {
                RandomState = _random.CaptureState(),
                ModuleState = _runtimeModule.CaptureState()
            };
        }

        public void RestoreState(object state)
        {
            if (state is not RuntimeState runtimeState)
            {
                _runtimeModule.RestoreState(null);
                return;
            }

            _random.RestoreState(runtimeState.RandomState);
            _runtimeModule.RestoreState(runtimeState.ModuleState);
        }
    }
}
