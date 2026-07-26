using System;
using System.Collections.Generic;
using Game.Application;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Presentation
{
    /// <summary>
    /// Owns Gameplay, UI, and Debug action maps plus keyboard/mouse and common
    /// gamepad bindings. UI and Gameplay are mutually exclusive.
    /// </summary>
    public sealed class M7InputRouter : MonoBehaviour, IInputRebindService
    {
        private InputActionAsset actions;
        private InputAction move;
        private InputAction navigate;
        private InputAction submit;
        private InputAction cancel;
        private InputAction pause;
        private InputAction debugLevelUp;
        private InputAction debugComplete;
        private bool initialized;
        private float stickDeadzone = 0.15f;

        public event Action<float> Navigate;
        public event Action Submit;
        public event Action Cancel;
        public event Action Pause;
        public event Action DebugLevelUp;
        public event Action DebugCompleteRun;

        public InputActionAsset Actions => actions;
        public InputActionMap GameplayMap => actions?.FindActionMap("Gameplay");
        public InputActionMap UiMap => actions?.FindActionMap("UI");
        public InputActionMap DebugMap => actions?.FindActionMap("Debug");
        public Vector2 Move
        {
            get
            {
                if (move == null || !move.enabled) return Vector2.zero;
                var value = move.ReadValue<Vector2>();
                var magnitude = value.magnitude;
                if (magnitude <= stickDeadzone) return Vector2.zero;
                if (magnitude <= 0f) return Vector2.zero;
                var scaled = Mathf.Clamp01((magnitude - stickDeadzone) / (1f - stickDeadzone));
                return value / magnitude * scaled;
            }
        }
        public bool IsGameplayMode => GameplayMap != null && GameplayMap.enabled;

        public void SetStickDeadzone(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return;
            stickDeadzone = Mathf.Clamp(value, 0f, 0.95f);
        }

        public void Initialize(InputActionAsset source = null)
        {
            if (initialized) throw new InvalidOperationException("M7InputRouter is already initialized.");
            actions = source != null ? Instantiate(source) : CreateDefaultActions();
            actions.name = "M7_RuntimeInputActions";
            move = Require("Gameplay", "Move");
            pause = Require("Gameplay", "Pause");
            navigate = Require("UI", "Navigate");
            submit = Require("UI", "Submit");
            cancel = Require("UI", "Cancel");
            debugLevelUp = Require("Debug", "LevelUp");
            debugComplete = Require("Debug", "CompleteRun");

            navigate.performed += OnNavigate;
            submit.performed += OnSubmit;
            cancel.performed += OnCancel;
            pause.performed += OnPause;
            debugLevelUp.performed += OnDebugLevelUp;
            debugComplete.performed += OnDebugComplete;
            DebugMap.Enable();
            SetGameplayMode(false);
            initialized = true;
        }

        public void SetGameplayMode(bool gameplay)
        {
            if (actions == null) return;
            if (gameplay)
            {
                UiMap.Disable();
                GameplayMap.Enable();
            }
            else
            {
                GameplayMap.Disable();
                UiMap.Enable();
            }
        }

        public bool ApplyBindingOverride(string actionName, int bindingIndex, string controlPath)
        {
            if (actions == null || string.IsNullOrWhiteSpace(actionName) || string.IsNullOrWhiteSpace(controlPath))
                return false;
            var action = actions.FindAction(actionName, false);
            if (action == null || bindingIndex < 0 || bindingIndex >= action.bindings.Count) return false;
            action.ApplyBindingOverride(bindingIndex, controlPath);
            return true;
        }

        public void RemoveAllBindingOverrides()
        {
            actions?.RemoveAllBindingOverrides();
        }

        /// <summary>Captures non-empty binding overrides as settings-safe pure data.</summary>
        public SavedBindingOverride[] CaptureBindingOverrides()
        {
            if (actions == null) return Array.Empty<SavedBindingOverride>();
            var captured = new List<SavedBindingOverride>();
            foreach (var map in actions.actionMaps)
            foreach (var action in map.actions)
            for (var index = 0; index < action.bindings.Count; index++)
            {
                var path = action.bindings[index].overridePath;
                if (!string.IsNullOrEmpty(path))
                    captured.Add(new SavedBindingOverride(map.name + "/" + action.name, index, path));
            }
            return captured.ToArray();
        }

        /// <summary>Applies persisted overrides through the normal rebind validation path.</summary>
        public void ApplyBindingOverrides(IReadOnlyList<SavedBindingOverride> overrides)
        {
            if (overrides == null) return;
            for (var index = 0; index < overrides.Count; index++)
            {
                var item = overrides[index];
                ApplyBindingOverride(item.ActionName, item.BindingIndex, item.ControlPath);
            }
        }

        public void SetGamepadVibration(float lowFrequency, float highFrequency, float intensity)
        {
            var gamepad = Gamepad.current;
            if (gamepad == null) return;
            var clamped = Mathf.Clamp01(intensity);
            gamepad.SetMotorSpeeds(Mathf.Clamp01(lowFrequency) * clamped, Mathf.Clamp01(highFrequency) * clamped);
        }

        private InputAction Require(string mapName, string actionName)
        {
            var action = actions.FindActionMap(mapName, true).FindAction(actionName, true);
            return action;
        }

        private void OnNavigate(InputAction.CallbackContext context)
        {
            var value = context.ReadValue<Vector2>();
            if (Mathf.Abs(value.y) > 0.5f) Navigate?.Invoke(value.y);
        }

        private void OnSubmit(InputAction.CallbackContext _) => Submit?.Invoke();
        private void OnCancel(InputAction.CallbackContext _) => Cancel?.Invoke();
        private void OnPause(InputAction.CallbackContext _) => Pause?.Invoke();
        private void OnDebugLevelUp(InputAction.CallbackContext _) => DebugLevelUp?.Invoke();
        private void OnDebugComplete(InputAction.CallbackContext _) => DebugCompleteRun?.Invoke();

        public static InputActionAsset CreateDefaultActions()
        {
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            var gameplay = asset.AddActionMap("Gameplay");
            var movement = gameplay.AddAction("Move", InputActionType.Value);
            movement.expectedControlType = "Vector2";
            movement.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            movement.AddBinding("<Gamepad>/leftStick", processors: "stickDeadzone(min=0.15,max=0.95)");
            gameplay.AddAction("Pause", InputActionType.Button)
                .AddBinding("<Keyboard>/escape");
            gameplay.FindAction("Pause").AddBinding("<Gamepad>/start");

            var ui = asset.AddActionMap("UI");
            var navigation = ui.AddAction("Navigate", InputActionType.Value);
            navigation.expectedControlType = "Vector2";
            navigation.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/rightArrow");
            navigation.AddBinding("<Gamepad>/dpad");
            navigation.AddBinding("<Gamepad>/leftStick", processors: "stickDeadzone(min=0.5,max=0.95)");
            ui.AddAction("Submit", InputActionType.Button).AddBinding("<Keyboard>/enter");
            ui.FindAction("Submit").AddBinding("<Keyboard>/space");
            ui.FindAction("Submit").AddBinding("<Gamepad>/buttonSouth");
            ui.AddAction("Cancel", InputActionType.Button).AddBinding("<Keyboard>/escape");
            ui.FindAction("Cancel").AddBinding("<Gamepad>/buttonEast");

            var debug = asset.AddActionMap("Debug");
            debug.AddAction("LevelUp", InputActionType.Button).AddBinding("<Keyboard>/f2");
            debug.FindAction("LevelUp").AddBinding("<Gamepad>/leftShoulder");
            debug.AddAction("CompleteRun", InputActionType.Button).AddBinding("<Keyboard>/f3");
            debug.FindAction("CompleteRun").AddBinding("<Gamepad>/rightShoulder");
            return asset;
        }

        private void OnDestroy()
        {
            if (!initialized) return;
            navigate.performed -= OnNavigate;
            submit.performed -= OnSubmit;
            cancel.performed -= OnCancel;
            pause.performed -= OnPause;
            debugLevelUp.performed -= OnDebugLevelUp;
            debugComplete.performed -= OnDebugComplete;
            if (Gamepad.current != null) Gamepad.current.SetMotorSpeeds(0f, 0f);
            actions.Disable();
            UnityObjectLifetime.Destroy(actions);
            initialized = false;
        }
    }
}
