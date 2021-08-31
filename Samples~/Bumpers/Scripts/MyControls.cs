// GENERATED AUTOMATICALLY FROM 'Assets/Samples/HiFi Spatialized Audio/0.2.0/Bumpers/MovementInput.inputactions'

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;

public class @MyControls : IInputActionCollection, IDisposable
{
    public InputActionAsset asset { get; }
    public @MyControls()
    {
        asset = InputActionAsset.FromJson(@"{
    ""name"": ""MovementInput"",
    ""maps"": [
        {
            ""name"": ""Movement"",
            ""id"": ""f2ce142e-fe7e-45b8-b6dd-0f096740680a"",
            ""actions"": [
                {
                    ""name"": ""ForwardBack"",
                    ""type"": ""Button"",
                    ""id"": ""643111d9-973c-4b1f-98f8-bc9c6d099af9"",
                    ""expectedControlType"": ""Button"",
                    ""processors"": """",
                    ""interactions"": """"
                },
                {
                    ""name"": ""Rotate"",
                    ""type"": ""Button"",
                    ""id"": ""e0ec7a24-d079-49cd-891c-0866648d75b9"",
                    ""expectedControlType"": ""Button"",
                    ""processors"": """",
                    ""interactions"": """"
                },
                {
                    ""name"": ""Strafe"",
                    ""type"": ""Button"",
                    ""id"": ""7fc98b0d-e13a-4049-aa98-e93c36903c7a"",
                    ""expectedControlType"": ""Button"",
                    ""processors"": """",
                    ""interactions"": """"
                }
            ],
            ""bindings"": [
                {
                    ""name"": ""wasd"",
                    ""id"": ""9d8ec9cd-4b83-4d94-b0f7-7bcb5007104b"",
                    ""path"": ""1DAxis"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""ForwardBack"",
                    ""isComposite"": true,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": ""negative"",
                    ""id"": ""2930091f-9b33-490c-868c-31f06f97df10"",
                    ""path"": ""<Keyboard>/s"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""ForwardBack"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": true
                },
                {
                    ""name"": ""positive"",
                    ""id"": ""048c7800-48a5-4ab4-b604-2fdbf691a94e"",
                    ""path"": ""<Keyboard>/w"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""ForwardBack"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": true
                },
                {
                    ""name"": ""arrows"",
                    ""id"": ""08337689-dee7-445a-a73d-15640b186bdc"",
                    ""path"": ""1DAxis"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""ForwardBack"",
                    ""isComposite"": true,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": ""negative"",
                    ""id"": ""69c5750a-8409-4200-a2d1-72eff1225663"",
                    ""path"": ""<Keyboard>/downArrow"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""ForwardBack"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": true
                },
                {
                    ""name"": ""positive"",
                    ""id"": ""0dc20b69-4e51-4b9d-844c-463868c2bfb7"",
                    ""path"": ""<Keyboard>/upArrow"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""ForwardBack"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": true
                },
                {
                    ""name"": ""wasd"",
                    ""id"": ""168a45ca-61ab-4ac8-9f7a-4f3eedb8ae24"",
                    ""path"": ""1DAxis"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Rotate"",
                    ""isComposite"": true,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": ""negative"",
                    ""id"": ""56a1b5bd-9950-4ccd-bdcf-b79f306e8ff1"",
                    ""path"": ""<Keyboard>/d"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Rotate"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": true
                },
                {
                    ""name"": ""positive"",
                    ""id"": ""480cf9be-248e-4f69-a95c-d7e1ec46ef15"",
                    ""path"": ""<Keyboard>/a"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Rotate"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": true
                },
                {
                    ""name"": ""arrows"",
                    ""id"": ""6bc274f6-b7cd-437d-8b86-5372a5efedcf"",
                    ""path"": ""1DAxis"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Rotate"",
                    ""isComposite"": true,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": ""negative"",
                    ""id"": ""ab2a995d-0fb6-4e13-9337-9378cc0c98f1"",
                    ""path"": ""<Keyboard>/leftArrow"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Rotate"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": true
                },
                {
                    ""name"": ""positive"",
                    ""id"": ""89934f72-f96a-4e05-970a-6ae9afcba1c9"",
                    ""path"": ""<Keyboard>/leftArrow"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Rotate"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": true
                },
                {
                    ""name"": ""wasd"",
                    ""id"": ""82337aea-60bc-4ac6-82cf-71c46652a1b5"",
                    ""path"": ""1DAxis"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Strafe"",
                    ""isComposite"": true,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": ""negative"",
                    ""id"": ""00912302-3f62-4540-abb2-ce040af582cd"",
                    ""path"": """",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Strafe"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": true
                },
                {
                    ""name"": ""positive"",
                    ""id"": ""3a16c48e-e323-4f99-8518-345412d53765"",
                    ""path"": """",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Strafe"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": true
                }
            ]
        }
    ],
    ""controlSchemes"": []
}");
        // Movement
        m_Movement = asset.FindActionMap("Movement", throwIfNotFound: true);
        m_Movement_ForwardBack = m_Movement.FindAction("ForwardBack", throwIfNotFound: true);
        m_Movement_Rotate = m_Movement.FindAction("Rotate", throwIfNotFound: true);
        m_Movement_Strafe = m_Movement.FindAction("Strafe", throwIfNotFound: true);
    }

    public void Dispose()
    {
        UnityEngine.Object.Destroy(asset);
    }

    public InputBinding? bindingMask
    {
        get => asset.bindingMask;
        set => asset.bindingMask = value;
    }

    public ReadOnlyArray<InputDevice>? devices
    {
        get => asset.devices;
        set => asset.devices = value;
    }

    public ReadOnlyArray<InputControlScheme> controlSchemes => asset.controlSchemes;

    public bool Contains(InputAction action)
    {
        return asset.Contains(action);
    }

    public IEnumerator<InputAction> GetEnumerator()
    {
        return asset.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public void Enable()
    {
        asset.Enable();
    }

    public void Disable()
    {
        asset.Disable();
    }

    // Movement
    private readonly InputActionMap m_Movement;
    private IMovementActions m_MovementActionsCallbackInterface;
    private readonly InputAction m_Movement_ForwardBack;
    private readonly InputAction m_Movement_Rotate;
    private readonly InputAction m_Movement_Strafe;
    public struct MovementActions
    {
        private @MyControls m_Wrapper;
        public MovementActions(@MyControls wrapper) { m_Wrapper = wrapper; }
        public InputAction @ForwardBack => m_Wrapper.m_Movement_ForwardBack;
        public InputAction @Rotate => m_Wrapper.m_Movement_Rotate;
        public InputAction @Strafe => m_Wrapper.m_Movement_Strafe;
        public InputActionMap Get() { return m_Wrapper.m_Movement; }
        public void Enable() { Get().Enable(); }
        public void Disable() { Get().Disable(); }
        public bool enabled => Get().enabled;
        public static implicit operator InputActionMap(MovementActions set) { return set.Get(); }
        public void SetCallbacks(IMovementActions instance)
        {
            if (m_Wrapper.m_MovementActionsCallbackInterface != null)
            {
                @ForwardBack.started -= m_Wrapper.m_MovementActionsCallbackInterface.OnForwardBack;
                @ForwardBack.performed -= m_Wrapper.m_MovementActionsCallbackInterface.OnForwardBack;
                @ForwardBack.canceled -= m_Wrapper.m_MovementActionsCallbackInterface.OnForwardBack;
                @Rotate.started -= m_Wrapper.m_MovementActionsCallbackInterface.OnRotate;
                @Rotate.performed -= m_Wrapper.m_MovementActionsCallbackInterface.OnRotate;
                @Rotate.canceled -= m_Wrapper.m_MovementActionsCallbackInterface.OnRotate;
                @Strafe.started -= m_Wrapper.m_MovementActionsCallbackInterface.OnStrafe;
                @Strafe.performed -= m_Wrapper.m_MovementActionsCallbackInterface.OnStrafe;
                @Strafe.canceled -= m_Wrapper.m_MovementActionsCallbackInterface.OnStrafe;
            }
            m_Wrapper.m_MovementActionsCallbackInterface = instance;
            if (instance != null)
            {
                @ForwardBack.started += instance.OnForwardBack;
                @ForwardBack.performed += instance.OnForwardBack;
                @ForwardBack.canceled += instance.OnForwardBack;
                @Rotate.started += instance.OnRotate;
                @Rotate.performed += instance.OnRotate;
                @Rotate.canceled += instance.OnRotate;
                @Strafe.started += instance.OnStrafe;
                @Strafe.performed += instance.OnStrafe;
                @Strafe.canceled += instance.OnStrafe;
            }
        }
    }
    public MovementActions @Movement => new MovementActions(this);
    public interface IMovementActions
    {
        void OnForwardBack(InputAction.CallbackContext context);
        void OnRotate(InputAction.CallbackContext context);
        void OnStrafe(InputAction.CallbackContext context);
    }
}
