// Copyright (c) 2024 Synty Studios Limited. All rights reserved.
//
// Use of this software is subject to the terms and conditions of the Synty Studios End User Licence Agreement (EULA)
// available at: https://syntystore.com/pages/end-user-licence-agreement
//
// Sample scripts are included only as examples and are not intended as production-ready.

using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace Synty.AnimationBaseLocomotion.Samples.InputSystem
{
    public class InputReader : MonoBehaviour, Controls.IPlayerActions
    {
        public Vector2 _mouseDelta;
        public Vector2 _moveComposite;

        public float _movementInputDuration;
        public bool _movementInputDetected;

        private Controls _controls;

        public Action onAimActivated;
        public Action onAimDeactivated;

        public Action onCrouchActivated;
        public Action onCrouchDeactivated;

        public Action onJumpPerformed;

        public Action onLockOnToggled;

        public Action onSprintActivated;
        public Action onSprintDeactivated;

        public Action onWalkToggled;

        public Action onAttackActivated;
        public Action onAttackDeactivated;

        // Bladehold addition: dismount-from-horse action (X / gamepad D-pad Up - the same keys as
        // SummonMount, so one button mounts and dismounts). Requires the Dismount action added to
        // Controls.inputactions and the Controls C# class regenerated; until then OnDismount is never
        // called (PlayerMount falls back to a direct X-key read).
        public Action onDismountPerformed;

        // Bladehold addition: start-wave action (T / gamepad D-pad Down), a plain Button with no
        // interaction, so started = press, performed = press, canceled = release. The wave flow
        // holds it for a set time to start a wave; StartWaveHeld tracks whether it is down.
        public Action onStartWaveStarted;
        public Action onStartWavePerformed;
        public Action onStartWaveCanceled;
        public bool StartWaveHeld { get; private set; }

        // Bladehold addition: interact action (E / gamepad West).
        public Action onInteractPerformed;

        // Bladehold addition: summon-mount action (X / gamepad D-pad Up). Shares its keys with Dismount:
        // on foot it summons, while riding PlayerSummonMount ignores it and Dismount fires.
        public Action onSummonMountPerformed;

        public bool IsAimPressed => _controls != null && _controls.Player.Aim.IsPressed();
        public bool IsAttackPressed => _controls != null && _controls.Player.Attack.IsPressed();


        /// <inheritdoc cref="OnEnable" />
        private void OnEnable()
        {
            if (_controls == null)
            {
                _controls = new Controls();
                _controls.Player.SetCallbacks(this);
            }

            _controls.Player.Enable();
        }

        /// <inheritdoc cref="OnDisable" />
        public void OnDisable()
        {
            _controls.Player.Disable();
        }

        /// <summary>
        ///     Defines the action to perform when the OnLook callback is called.
        /// </summary>
        /// <param name="context">The context of the callback.</param>
        public void OnLook(InputAction.CallbackContext context)
        {
            _mouseDelta = context.ReadValue<Vector2>();
        }

        /// <summary>
        ///     Defines the action to perform when the OnMove callback is called.
        /// </summary>
        /// <param name="context">The context of the callback.</param>
        public void OnMove(InputAction.CallbackContext context)
        {
            _moveComposite = context.ReadValue<Vector2>();
            _movementInputDetected = _moveComposite.magnitude > 0;
        }

        public void OnJump(InputAction.CallbackContext context)
        {
            // Jumping disabled by user request.
            /*
            if (!context.performed)
            {
                return;
            }

            onJumpPerformed?.Invoke();
            */
        }

        /// <summary>
        ///     Defines the action to perform when the OnToggleWalk callback is called.
        /// </summary>
        /// <param name="context">The context of the callback.</param>
        public void OnToggleWalk(InputAction.CallbackContext context)
        {
            if (!context.performed)
            {
                return;
            }

            onWalkToggled?.Invoke();
        }

        /// <summary>
        ///     Defines the action to perform when the OnSprint callback is called.
        /// </summary>
        /// <param name="context">The context of the callback.</param>
        public void OnSprint(InputAction.CallbackContext context)
        {
            // Sprint disabled per user request, logic preserved in case it's added back
            
            if (context.started)
            {
                onSprintActivated?.Invoke();
            }
            else if (context.canceled)
            {
                onSprintDeactivated?.Invoke();
            }
            
        }

        /// <summary>
        ///     Defines the action to perform when the OnCrouch callback is called.
        /// </summary>
        /// <param name="context">The context of the callback.</param>
        public void OnCrouch(InputAction.CallbackContext context)
        {
            if (context.started)
            {
                onCrouchActivated?.Invoke();
            }
            else if (context.canceled)
            {
                onCrouchDeactivated?.Invoke();
            }
        }

        /// <summary>
        ///     Defines the action to perform when the OnAim callback is called.
        /// </summary>
        /// <param name="context">The context of the callback.</param>
        public void OnAim(InputAction.CallbackContext context)
        {
            // Bladehold: performed, not started. On an analog trigger (LT) a Button action "starts" at the
            // first hair of travel but only reads IsPressed past the press point, so aiming on `started`
            // let the aim weapons' IsAimPressed check end the aim again the very next frame.
            if (context.performed)
            {
                if (CursorLockManager.IsCursorUnlocked) return;
                onAimActivated?.Invoke();
            }

            if (context.canceled)
            {
                onAimDeactivated?.Invoke();
            }
        }

        /// <summary>
        ///     Defines the action to perform when the OnLockOn callback is called.
        /// </summary>
        /// <param name="context">The context of the callback.</param>
        public void OnLockOn(InputAction.CallbackContext context)
        {
            if (!context.performed)
            {
                return;
            }

            onLockOnToggled?.Invoke();
            onSprintDeactivated?.Invoke();
        }

        /// <summary>
        ///     Bladehold: re-raises an attack press for a button that's still held, so a press buffered
        ///     during the melee cooldown starts its attack the moment the cooldown ends.
        /// </summary>
        public void ReplayAttackPress()
        {
            if (!IsAttackPressed || CursorLockManager.IsCursorUnlocked) return;
            onAttackActivated?.Invoke();
        }

        public void OnAttack(InputAction.CallbackContext context)
        {
            // Bladehold: performed, not started (see OnAim). On RT `started` fired at the first hair of
            // travel while IsAttackPressed still read false, so the charge auto-released into a swing
            // before the trigger was let go.
            if (context.performed)
            {
                if (CursorLockManager.IsCursorUnlocked) return;
                onAttackActivated?.Invoke();
            }

            if (context.canceled)
            {
                onAttackDeactivated?.Invoke();
            }
        }

        // Bladehold addition: see onDismountPerformed above. Becomes an IPlayerActions member once
        // the Controls class is regenerated with the Dismount action; harmlessly unused before then.
        public void OnDismount(InputAction.CallbackContext context)
        {
            if (!context.performed)
            {
                return;
            }

            onDismountPerformed?.Invoke();
        }

        public void OnUltimate(InputAction.CallbackContext context)
        {
            // Handled directly by PlayerUltimateController listening to the action.
            // Implemented here only to satisfy the IPlayerActions interface generated from Controls.inputactions.
        }

        public void OnStartWave(InputAction.CallbackContext context)
        {
            if (context.started)
            {
                StartWaveHeld = true;
                onStartWaveStarted?.Invoke();
            }
            else if (context.performed)
            {
                StartWaveHeld = true;
                onStartWavePerformed?.Invoke();
            }
            else if (context.canceled)
            {
                StartWaveHeld = false;
                onStartWaveCanceled?.Invoke();
            }
        }

        public void OnInteract(InputAction.CallbackContext context)
        {
            if (!context.performed)
            {
                return;
            }

            onInteractPerformed?.Invoke();
        }

        public void OnSummonMount(InputAction.CallbackContext context)
        {
            if (!context.performed)
            {
                return;
            }

            onSummonMountPerformed?.Invoke();
        }
    }
}
