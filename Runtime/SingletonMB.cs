using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Neonalig.Core
{
    /// <summary>
    /// A singleton MonoBehaviour class.
    /// </summary>
    /// <typeparam name="T">The type of the singleton.</typeparam>
    public abstract class SingletonMB<T> : MonoBehaviour where T : SingletonMB<T>
    {
        private static T? _instance;
        private static event InstanceAssignedEventHandler? _instanceAssignedEventBuffer;

        /// <summary>
        /// Event signature for the <see cref="InstanceAssigned"/> event.
        /// </summary>
        public delegate void InstanceAssignedEventHandler(T instance);

        /// <summary>
        /// Event that is invoked when the singleton instance is assigned.<br/>
        /// If the instance is already assigned, the event will be invoked immediately.
        /// </summary>
        public static event InstanceAssignedEventHandler InstanceAssigned
        {
            add
            {
                if (_instance != null)
                    value(_instance);
                else
                    _instanceAssignedEventBuffer += value;
            }
            remove => _instanceAssignedEventBuffer -= value;
        }

        /// <summary>
        /// Override this method to perform any initialization logic when the singleton is created.<br/>
        /// This method is called in the Awake method of the singleton, only if the instance is not already assigned (as otherwise this object is being destroyed).
        /// </summary>
        protected virtual void OnAwake() { }

        /// <summary>
        /// Awake method that initializes the singleton instance.<br/>
        /// The base method must always be called in derived classes. Use <see cref="OnAwake"/> for custom initialization logic.
        /// </summary>
        protected void Awake()
        {
            if (_instance != null)
            {
                // Multiple instances detected - destroy this instance
                Debug.LogWarning(
                    $"[{typeof(T).Name}] Other components are present on this singleton GameObject. " +
                    $"Singleton {typeof(T).Name} expects to be the only component on its GameObject. " +
                    "Destroying this component to avoid conflicts."
                );

                Component[] components = GetComponents<Component>();
                if (components.All(c => c is T or Transform))
                {
                    // If this is the only component, destroy the GameObject
                    Destroy(gameObject);
                }
                else
                {
                    // Otherwise, just destroy this component
                    Destroy(this);
                }

                return;
            }

            _instance = (T)this;
#if UNITY_EDITOR
            SingletonCleanup.OnCleanup += Cleanup;
#endif

            // Debug.Log($"Singleton instance of {typeof(T).Name} assigned: {instance.GetInstanceID()}", instance);
            OnAwake();

            if (_instanceAssignedEventBuffer != null)
            {
                foreach (var handler in _instanceAssignedEventBuffer.GetInvocationList())
                {
                    var typedHandler = ((InstanceAssignedEventHandler)handler);
                    try
                    {
                        typedHandler(_instance);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[{typeof(T).Name}] Error invoking InstanceAssigned handler, skipping. See below log for details.");
                        Debug.LogException(e);
                    }
                }
                _instanceAssignedEventBuffer = null;
            }
        }

#if UNITY_EDITOR
        private void Cleanup()
        {
            if (_instance == this)
            {
                _instance = null;
                _instanceAssignedEventBuffer = null;
            }
        }
#endif

        /// <summary>
        /// Returns the instance if present; otherwise logs an error and throws.
        /// Prefer <see cref="HasInstance"/> / <see cref="InstanceOrNull"/> / <see cref="TryGetInstance(out T?)"/> for presence checks.
        /// </summary>
        public static T Instance
        {
            get
            {
                if (_instance != null) return _instance;
                // Keep this strict so accidental access is loud.
                var type = typeof(T).Name;
                Debug.LogError($"[{typeof(T).Name}] No instance of {type} found in scene (Awake not called yet, or it was destroyed).");
                // Throw to fail fast in dev
                #if UNITY_EDITOR
                throw new InvalidOperationException($"Singleton '{type}' is not available.");
                #else
                return null!;
                #endif
            }
        }

        /// <summary>Returns whether an instance exists.</summary>
        [MemberNotNullWhen(true, nameof(_instance))]
        public static bool HasInstance => _instance != null;

        /// <summary>
        /// Returns the instance or null without logging. Use this for benign presence checks.
        /// </summary>
        public static T? InstanceOrNull => _instance;

        /// <summary>
        /// Tries to get the instance without logging.
        /// </summary>
        public static bool TryGetInstance([NotNullWhen(true)] out T? value)
        {
            value = _instance;
            return value != null;
        }

        /// <summary>
        /// Like <see cref="Instance"/> but returns the instance (never null) after logging a clear error if missing.
        /// Useful when you intentionally want a loud failure at the call site.
        /// </summary>
        public static T RequireInstance()
        {
            if (_instance != null) return _instance;
            Debug.LogError($"[{typeof(T).Name}] RequireInstance failed: no instance of {typeof(T).Name} present.");
            throw new InvalidOperationException($"Singleton '{typeof(T).Name}' is not available.");
        }

        /// <summary>
        /// This method is called when the singleton is destroyed, either by Unity or manually.
        /// It sets the instance to null if it is the current instance.<br/><br/>
        /// If you override this method, make sure to call the base method to ensure the instance is cleared properly.
        /// </summary>
        protected virtual void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }

#if UNITY_EDITOR
            SingletonCleanup.OnCleanup -= Cleanup;
#endif
        }
    }

    /// <summary>
    /// Wait for a singleton to be initialised before continuing execution in a coroutine.
    /// </summary>
    /// <typeparam name="T"> The type of singleton to wait for. Must implement <see cref="SingletonMB{T}"/>. </typeparam>
    public sealed class WaitForSingletonMB<T> : CustomYieldInstruction where T : SingletonMB<T>
    {
        private bool _instanceAssigned = false;

        /// <summary>
        /// The instance of the singleton that has been assigned.<br/>
        /// This will be null until the instance is assigned, at which point it will hold the assigned instance.
        /// </summary>
        public T? Instance { get; private set; }

        public WaitForSingletonMB()
        {
            SingletonMB<T>.InstanceAssigned += OnInstanceAssigned;
        }

        public override bool keepWaiting
        {
            get => !_instanceAssigned;
        }

        private void OnInstanceAssigned(T e)
        {
            _instanceAssigned = true;
            Instance = e;
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// Utility class for cleaning up singleton instances in the editor.
    /// </summary>
    internal static class SingletonCleanup
    {
        /// <summary>
        /// Event that is invoked when the editor is exiting play mode.
        /// </summary>
        public static event Action? OnCleanup;

        /// <summary>
        /// Cleans up all instances of SingletonMB in the scene.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            OnCleanup?.Invoke();
            OnCleanup = null;
        }
    }
#endif
}
