using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using YARG.Core.Input;
using YARG.Input;
using YARG.Menu.Persistent;
using YARG.Player;

namespace YARG.Menu.Navigation
{
    public readonly struct NavigationContext
    {
        /// <summary>
        /// The type of navigation.
        /// </summary>
        public readonly MenuAction Action;

        /// <summary>
        /// The <see cref="YargPlayer"/> that this event was invoked from.
        /// </summary>
        public readonly YargPlayer Player;

        /// <summary>
        /// Whether or not this action is a repeat.
        /// </summary>
        public readonly bool IsRepeat;

        public NavigationContext(MenuAction action, YargPlayer player, bool repeat = false)
        {
            Action = action;
            Player = player;
            IsRepeat = repeat;
        }

        public bool IsSameAs(NavigationContext other)
        {
            return other.Action == Action && other.Player == Player;
        }

        public NavigationContext AsRepeat()
        {
            return new(Action, Player, true);
        }
    }

    public class Navigator : MonoSingleton<Navigator>
    {
        private const float INPUT_REPEAT_TIME = 0.035f;
        private const float INPUT_REPEAT_COOLDOWN = 0.5f;

        private static readonly HashSet<MenuAction> RepeatActions = new()
        {
            MenuAction.Up,
            MenuAction.Down,
            MenuAction.Left,
            MenuAction.Right,
        };

        private class HoldContext
        {
            public readonly NavigationContext Context;
            public float Timer = INPUT_REPEAT_COOLDOWN;

            public HoldContext(NavigationContext context)
            {
                Context = context;
            }
        }

        public event Action<NavigationContext> NavigationEvent;

        private readonly List<HoldContext> _heldInputs = new();
        private readonly Stack<NavigationScheme> _schemeStack = new();

        private void Start()
        {
            UpdateHelpBar();
            InputManager.MenuInput += ProcessInput;
        }

        private void Update()
        {
            // Update held inputs
            foreach (var heldInput in _heldInputs)
            {
                heldInput.Timer -= Time.unscaledDeltaTime;

                if (heldInput.Timer <= 0f)
                {
                    heldInput.Timer = INPUT_REPEAT_TIME;
                    InvokeNavigationEvent(heldInput.Context.AsRepeat());
                }
            }

            // TODO: Keyboard inputs for menus
            // UpdateKeyboardInput();
        }

        private void ProcessInput(YargPlayer player, ref GameInput input)
        {
            var action = (MenuAction)input.Action;
            var context = new NavigationContext(action, player);
            if (input.Button)
                StartNavigationHold(context);
            else
                EndNavigationHold(context);
        }

        private void StartNavigationHold(NavigationContext context)
        {
            // Skip if the input is already being held
            if (_heldInputs.Any(i => i.Context.IsSameAs(context)))
            {
                return;
            }

            InvokeNavigationEvent(context);

            if (RepeatActions.Contains(context.Action))
                _heldInputs.Add(new HoldContext(context));
        }

        private void EndNavigationHold(NavigationContext context)
        {
            _heldInputs.RemoveAll(i => i.Context.IsSameAs(context));
        }

        public bool IsHeld(MenuAction action)
        {
            return _heldInputs.Any(i => i.Context.Action == action);
        }

        private void InvokeNavigationEvent(NavigationContext ctx)
        {
            NavigationEvent?.Invoke(ctx);

            if (_schemeStack.Count > 0)
            {
                _schemeStack.Peek().InvokeFuncs(ctx.Action);
            }
        }

        public void PushScheme(NavigationScheme scheme)
        {
            _schemeStack.Push(scheme);
            UpdateHelpBar();
        }

        public void PopScheme()
        {
            _schemeStack.Pop();
            UpdateHelpBar();
        }

        public void PopAllSchemes()
        {
            _schemeStack.Clear();
            UpdateHelpBar();
        }

        public void ForceHideMusicPlayer()
        {
            HelpBar.Instance.MusicPlayer.gameObject.SetActive(false);
        }

        private void UpdateHelpBar()
        {
            if (_schemeStack.Count <= 0)
            {
                HelpBar.Instance.Reset();
            }
            else
            {
                HelpBar.Instance.SetInfoFromScheme(_schemeStack.Peek());
            }
        }
    }
}