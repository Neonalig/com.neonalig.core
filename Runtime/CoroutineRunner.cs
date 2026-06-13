using System;
using System.Collections;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Neonalig.Core
{
    /// <summary>
    /// Persistent singleton for running coroutines that shouldn't be interrupted by scene object lifecycle.
    /// Automatically creates itself at runtime (no scene instance required).
    /// </summary>
    public sealed class CoroutineRunner : SingletonMB<CoroutineRunner>
    {
        private const string _RUNNER_NAME = "[CoroutineRunner]";
        private static bool _isQuitting;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            // Required when domain reload is disabled.
            _isQuitting = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void BootstrapBeforeSceneLoad()
        {
            // Ensure the runner exists before any scene Awake() calls.
            EnsureInstance();
        }

        private void OnApplicationQuit()
        {
            _isQuitting = true;
        }

        protected override void OnAwake()
        {
            base.OnAwake();
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// Ensures the CoroutineRunner singleton exists, creating it if necessary.
        /// Safe to call from anywhere (including static init / Awake).
        /// </summary>
        public static void EnsureInstance()
        {
            if (_isQuitting)
                return;

            if (HasInstance)
                return;

            // Try to find an existing one first (covers weird execution orders / manual placement).
            var existing = FindAnyObjectByType<CoroutineRunner>();
            if (existing != null)
            {
                // Touch Instance path to let SingletonMB register it if needed.
                _ = existing;
                return;
            }

            var go = new GameObject(_RUNNER_NAME)
            {
                hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor
            };

            // Adding the component will trigger Awake immediately, which should register Instance via SingletonMB.
            go.AddComponent<CoroutineRunner>();
        }

        public static MonoBehaviour Target
        {
            get
            {
                EnsureInstance();
                return Instance;
            }
        }

        /// <summary>
        /// Starts a coroutine on the runner.
        /// Runner is created automatically if missing.
        /// </summary>
        public static new CoroutineHandle StartCoroutine(IEnumerator routine)
        {
            if  (!Application.isPlaying)
                throw new InvalidOperationException("CoroutineRunner cannot start coroutines outside of play mode.");

            if (routine == null)
                throw new ArgumentNullException(nameof(routine));

            if (_isQuitting)
            {
                Debug.LogWarning("[CoroutineRunner] Attempted to start a coroutine while the application is quitting.");
                return CoroutineHandle.Completed;
            }

            EnsureInstance();

            var handle = new CoroutineHandle();
            var unity = Target.StartCoroutine(handle.Wrap(routine));
            handle.Attach(unity);
            return handle;
        }

        /// <summary>
        /// Stops a coroutine previously started via CoroutineRunner.
        /// </summary>
        public static void StopCoroutine(CoroutineHandle? handle)
        {
            if (handle == null)
                return;

            if (_isQuitting)
                return;

            var unity = handle.UnityCoroutine;
            if (unity == null)
                return;

            if (!HasInstance)
                return;

            Target.StopCoroutine(unity);
            handle.MarkStopped();
        }

        /// <summary>
        /// Stops a raw Unity coroutine on this runner (if it exists).
        /// </summary>
        public static new void StopCoroutine(Coroutine? coroutine)
        {
            if (coroutine == null)
                return;

            if (_isQuitting)
                return;

            if (!HasInstance)
                return;

            Target.StopCoroutine(coroutine);
        }
    }

    /// <summary>
    /// Handle for a coroutine started via CoroutineRunner.
    /// </summary>
    public sealed class CoroutineHandle : CustomYieldInstruction
    {
        private bool _completed;

        public static CoroutineHandle Completed { get; } = new() { _completed = true };

        public Coroutine? UnityCoroutine { get; private set; }

        public override bool keepWaiting => !_completed;

        internal CoroutineHandle() { }

        internal void Attach(Coroutine unityCoroutine)
        {
            UnityCoroutine = unityCoroutine;
        }

        internal IEnumerator Wrap(IEnumerator routine)
        {
            // Run the actual routine.
            yield return routine;

            // If we were stopped explicitly, we still consider the handle "done".
            _completed = true;
        }

        internal void MarkStopped()
        {
            WasStopped = true;
            _completed = true;
            UnityCoroutine = null;
        }

        /// <summary>
        /// Stops the coroutine if it's still running.
        /// </summary>
        public void Stop()
        {
            if (_completed)
                return;

            CoroutineRunner.StopCoroutine(this);
        }

        /// <summary>
        /// True if Stop() was called (best-effort; Unity does not signal cancellation to the enumerator).
        /// </summary>
        public bool WasStopped { get; private set; }
    }
}