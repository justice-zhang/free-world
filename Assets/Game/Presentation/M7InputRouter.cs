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
        private InputAction map;
        private InputAction interact;
        private InputAction tab;
        private InputAction page;
        private InputAction debugLevelUp;
        private InputAction debugComplete;
        private bool initialized;
        private float stickDeadzone = 0.15f;
        private string lastRebindDiagnosticKey = string.Empty;

        public event Action<float> Navigate;
        public event Action Submit;
        public event Action Cancel;
        public event Action Pause;
        public event Action Map;
        public event Action<float> Tab;
        public event Action<float> Page;
        public event Action FocusRestoreRequested;
        public event Action GamepadDisconnected;
        public event Action DebugLevelUp;
        public event Action DebugCompleteRun;

        public InputActionAsset Actions => actions;
        public InputActionMap GameplayMap => actions?.FindActionMap("Gameplay");
        public InputActionMap UiMap => actions?.FindActionMap("UI");
        public InputActionMap DebugMap => actions?.FindActionMap("Debug");
        public string LastRebindDiagnosticKey => lastRebindDiagnosticKey;
        public bool DebugEnabled => DebugMap != null && DebugMap.enabled;
        public bool InteractHeld => interact != null && interact.enabled && interact.IsPressed();
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
            EnsureContractActions();
            move = Require("Gameplay", "Move");
            pause = Require("Gameplay", "Pause");
            map = Require("Gameplay", "Map");
            interact = Require("Gameplay", "Interact");
            navigate = Require("UI", "Navigate");
            submit = Require("UI", "Submit");
            cancel = Require("UI", "Cancel");
            tab = Require("UI", "Tab");
            page = Require("UI", "Page");
            debugLevelUp = Require("Debug", "LevelUp");
            debugComplete = Require("Debug", "CompleteRun");

            navigate.performed += OnNavigate;
            submit.performed += OnSubmit;
            cancel.performed += OnCancel;
            pause.performed += OnPause;
            map.performed += OnMap;
            tab.performed += OnTab;
            page.performed += OnPage;
            debugLevelUp.performed += OnDebugLevelUp;
            debugComplete.performed += OnDebugComplete;
            if (Debug.isDebugBuild) DebugMap.Enable();
            else DebugMap.Disable();
            InputSystem.onDeviceChange += OnDeviceChange;
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
            lastRebindDiagnosticKey = string.Empty;
            if (actions == null || string.IsNullOrWhiteSpace(actionName) || string.IsNullOrWhiteSpace(controlPath))
            {
                lastRebindDiagnosticKey = "ui.qinglan.rebind.invalid";
                return false;
            }
            var action = actions.FindAction(actionName, false);
            if (action == null || bindingIndex < 0 || bindingIndex >= action.bindings.Count)
            {
                lastRebindDiagnosticKey = "ui.qinglan.rebind.invalid";
                return false;
            }
            foreach (var actionMap in actions.actionMaps)
            foreach (var otherAction in actionMap.actions)
            for (var index = 0; index < otherAction.bindings.Count; index++)
            {
                if (otherAction == action && index == bindingIndex) continue;
                var binding = otherAction.bindings[index];
                if (binding.isComposite) continue;
                if (string.Equals(binding.effectivePath, controlPath, StringComparison.OrdinalIgnoreCase))
                {
                    lastRebindDiagnosticKey = "ui.qinglan.rebind.conflict";
                    return false;
                }
            }
            action.ApplyBindingOverride(bindingIndex, controlPath);
            lastRebindDiagnosticKey = "ui.qinglan.rebind.applied";
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

        private void EnsureContractActions()
        {
            var gameplay = actions.FindActionMap("Gameplay", false) ?? actions.AddActionMap("Gameplay");
            var movement = gameplay.FindAction("Move", false);
            if (movement == null)
            {
                movement = gameplay.AddAction("Move", InputActionType.Value);
                movement.expectedControlType = "Vector2";
                movement.AddCompositeBinding("2DVector")
                    .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                    .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
                movement.AddBinding("<Gamepad>/leftStick", processors: "stickDeadzone(min=0.15,max=0.95)");
            }
            if (!HasBinding(movement, "<Keyboard>/w"))
                movement.AddCompositeBinding("2DVector")
                    .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                    .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            if (!HasBinding(movement, "<Keyboard>/upArrow"))
                movement.AddCompositeBinding("2DVector")
                    .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                    .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            if (!HasBinding(movement, "<Gamepad>/leftStick"))
                movement.AddBinding("<Gamepad>/leftStick", processors: "stickDeadzone(min=0.15,max=0.95)");
            EnsureButton(gameplay, "Pause", "<Keyboard>/escape", "<Gamepad>/start");
            EnsureButton(gameplay, "Map", "<Keyboard>/m", "<Gamepad>/select");
            EnsureButton(gameplay, "Interact", "<Keyboard>/e", "<Gamepad>/buttonNorth");

            var ui = actions.FindActionMap("UI", false) ?? actions.AddActionMap("UI");
            var navigation = ui.FindAction("Navigate", false);
            if (navigation == null)
            {
                navigation = ui.AddAction("Navigate", InputActionType.Value);
                navigation.expectedControlType = "Vector2";
                navigation.AddCompositeBinding("2DVector")
                    .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                    .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
                navigation.AddBinding("<Gamepad>/dpad");
                navigation.AddBinding("<Gamepad>/leftStick", processors: "stickDeadzone(min=0.5,max=0.95)");
            }
            EnsureBinding(navigation, "<Gamepad>/dpad");
            if (!HasBinding(navigation, "<Gamepad>/leftStick"))
                navigation.AddBinding("<Gamepad>/leftStick", processors: "stickDeadzone(min=0.5,max=0.95)");
            if (!HasBinding(navigation, "<Mouse>/scroll")) navigation.AddBinding("<Mouse>/scroll");
            EnsureButton(ui, "Submit", "<Keyboard>/enter", "<Gamepad>/buttonSouth");
            EnsureButton(ui, "Cancel", "<Keyboard>/escape", "<Gamepad>/buttonEast");
            EnsureBinding(ui.FindAction("Submit", true), "<Mouse>/leftButton");
            EnsureBinding(ui.FindAction("Cancel", true), "<Mouse>/rightButton");
            EnsureAxis(ui, "Tab", "<Keyboard>/q", "<Keyboard>/e", "<Gamepad>/leftShoulder", "<Gamepad>/rightShoulder");
            EnsureAxis(ui, "Page", "<Keyboard>/pageUp", "<Keyboard>/pageDown", "<Gamepad>/leftTrigger", "<Gamepad>/rightTrigger");

            var debug = actions.FindActionMap("Debug", false) ?? actions.AddActionMap("Debug");
            EnsureButton(debug, "LevelUp", "<Keyboard>/f2", "<Gamepad>/leftShoulder");
            EnsureButton(debug, "CompleteRun", "<Keyboard>/f3", "<Gamepad>/rightShoulder");
        }

        private static void EnsureButton(InputActionMap mapValue, string name, string keyboardPath, string gamepadPath)
        {
            var action = mapValue.FindAction(name, false) ?? mapValue.AddAction(name, InputActionType.Button);
            EnsureBinding(action, keyboardPath);
            EnsureBinding(action, gamepadPath);
        }

        private static void EnsureAxis(
            InputActionMap mapValue,
            string name,
            string keyboardNegative,
            string keyboardPositive,
            string gamepadNegative,
            string gamepadPositive)
        {
            var action = mapValue.FindAction(name, false) ?? mapValue.AddAction(name, InputActionType.Value);
            if (!HasBinding(action, keyboardNegative) || !HasBinding(action, keyboardPositive))
                action.AddCompositeBinding("1DAxis").With("Negative", keyboardNegative).With("Positive", keyboardPositive);
            if (!HasBinding(action, gamepadNegative) || !HasBinding(action, gamepadPositive))
                action.AddCompositeBinding("1DAxis").With("Negative", gamepadNegative).With("Positive", gamepadPositive);
        }

        private static bool HasBinding(InputAction action, string path)
        {
            for (var index = 0; index < action.bindings.Count; index++)
                if (string.Equals(action.bindings[index].path, path, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static void EnsureBinding(InputAction action, string path)
        {
            if (!HasBinding(action, path)) action.AddBinding(path);
        }

        private void OnNavigate(InputAction.CallbackContext context)
        {
            var value = context.ReadValue<Vector2>();
            if (Mathf.Abs(value.y) > 0.5f) Navigate?.Invoke(value.y);
        }

        private void OnSubmit(InputAction.CallbackContext _) => Submit?.Invoke();
        private void OnCancel(InputAction.CallbackContext _) => Cancel?.Invoke();
        private void OnPause(InputAction.CallbackContext _) => Pause?.Invoke();
        private void OnMap(InputAction.CallbackContext _) => Map?.Invoke();
        private void OnTab(InputAction.CallbackContext context)
        {
            var value = context.ReadValue<float>();
            if (Mathf.Abs(value) > 0.5f) Tab?.Invoke(value);
        }
        private void OnPage(InputAction.CallbackContext context)
        {
            var value = context.ReadValue<float>();
            if (Mathf.Abs(value) > 0.5f) Page?.Invoke(value);
        }
        private void OnDebugLevelUp(InputAction.CallbackContext _) => DebugLevelUp?.Invoke();
        private void OnDebugComplete(InputAction.CallbackContext _) => DebugCompleteRun?.Invoke();

        private void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (!(device is Gamepad) ||
                (change != InputDeviceChange.Disconnected && change != InputDeviceChange.Removed &&
                 change != InputDeviceChange.Reconnected && change != InputDeviceChange.Added)) return;
            if (change == InputDeviceChange.Disconnected || change == InputDeviceChange.Removed)
            {
                ((Gamepad)device).SetMotorSpeeds(0f, 0f);
                GamepadDisconnected?.Invoke();
            }
            FocusRestoreRequested?.Invoke();
        }

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
            movement.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/rightArrow");
            movement.AddBinding("<Gamepad>/leftStick", processors: "stickDeadzone(min=0.15,max=0.95)");
            gameplay.AddAction("Pause", InputActionType.Button)
                .AddBinding("<Keyboard>/escape");
            gameplay.FindAction("Pause").AddBinding("<Gamepad>/start");
            gameplay.AddAction("Map", InputActionType.Button).AddBinding("<Keyboard>/m");
            gameplay.FindAction("Map").AddBinding("<Gamepad>/select");
            gameplay.AddAction("Interact", InputActionType.Button).AddBinding("<Keyboard>/e");
            gameplay.FindAction("Interact").AddBinding("<Gamepad>/buttonNorth");

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
            navigation.AddBinding("<Mouse>/scroll");
            ui.AddAction("Submit", InputActionType.Button).AddBinding("<Keyboard>/enter");
            ui.FindAction("Submit").AddBinding("<Keyboard>/space");
            ui.FindAction("Submit").AddBinding("<Gamepad>/buttonSouth");
            ui.FindAction("Submit").AddBinding("<Mouse>/leftButton");
            ui.AddAction("Cancel", InputActionType.Button).AddBinding("<Keyboard>/escape");
            ui.FindAction("Cancel").AddBinding("<Gamepad>/buttonEast");
            ui.FindAction("Cancel").AddBinding("<Mouse>/rightButton");
            var tabAction = ui.AddAction("Tab", InputActionType.Value);
            tabAction.AddCompositeBinding("1DAxis")
                .With("Negative", "<Keyboard>/q")
                .With("Positive", "<Keyboard>/e");
            tabAction.AddCompositeBinding("1DAxis")
                .With("Negative", "<Gamepad>/leftShoulder")
                .With("Positive", "<Gamepad>/rightShoulder");
            var pageAction = ui.AddAction("Page", InputActionType.Value);
            pageAction.AddCompositeBinding("1DAxis")
                .With("Negative", "<Keyboard>/pageUp")
                .With("Positive", "<Keyboard>/pageDown");
            pageAction.AddCompositeBinding("1DAxis")
                .With("Negative", "<Gamepad>/leftTrigger")
                .With("Positive", "<Gamepad>/rightTrigger");

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
            map.performed -= OnMap;
            tab.performed -= OnTab;
            page.performed -= OnPage;
            debugLevelUp.performed -= OnDebugLevelUp;
            debugComplete.performed -= OnDebugComplete;
            InputSystem.onDeviceChange -= OnDeviceChange;
            if (Gamepad.current != null) Gamepad.current.SetMotorSpeeds(0f, 0f);
            actions.Disable();
            UnityObjectLifetime.Destroy(actions);
            initialized = false;
        }
    }
}
