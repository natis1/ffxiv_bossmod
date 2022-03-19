﻿using Dalamud.Interface;
using Dalamud.Utility;
using ImGuiNET;
using System;
using System.Linq;
using System.Numerics;

namespace BossMod
{
    // class that is responsible for drawing boss modules in game in a separate window
    class BossModuleManagerGame : BossModuleManager
    {
        private WindowManager.Window? _mainWindow;
        private BossModule? _drawnModule;

        public BossModuleManagerGame(WorldState ws, ConfigNode settings)
            : base(ws, settings)
        {
        }

        protected override void RefreshConfigOrModules()
        {
            // create or destroy main window if needed
            if (_mainWindow != null && LoadedModules.Count == 0)
            {
                Service.Log("[BMM] Closing main window, since there are no more loaded modules");
                WindowManager.CloseWindow(_mainWindow);
            }
            else if (_mainWindow == null && LoadedModules.Count > 0)
            {
                Service.Log("[BMM] Creating main window, since there are now loaded modules");
                _mainWindow = WindowManager.CreateWindow("Boss module", DrawMainWindow, MainWindowClosed, MainWindowClosedByUser);
                _mainWindow.SizeHint = new(400, 400);
            }

            // update main window properties
            SetDrawnModule(ActiveModule ?? (LoadedModules.Count == 1 ? LoadedModules.SingleOrDefault() : null));
            if (_mainWindow != null)
            {
                _mainWindow.Flags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;
                if (Config.TrishaMode)
                    _mainWindow.Flags |= ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoBackground;
                if (Config.Lock)
                    _mainWindow.Flags |= ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoInputs;
            }
        }

        private void SetDrawnModule(BossModule? m)
        {
            _drawnModule = m;
            if (_mainWindow != null)
                _mainWindow.Title = m != null ? $"Boss module ({m.GetType().Name})" : "Bosses with modules nearby";
        }

        private void DrawMainWindow()
        {
            if (_drawnModule != null)
            {
                try
                {
                    BossModule.MovementHints? movementHints = Config.ShowWorldArrows ? new() : null;
                    _drawnModule.Draw(Config.RotateArena ? (Camera.Instance?.CameraAzimuth ?? 0) : 0, PartyState.PlayerSlot, movementHints);
                    DrawMovementHints(movementHints);
                }
                catch (Exception ex)
                {
                    Service.Log($"Boss module crashed: {ex}");
                    SetDrawnModule(null);
                }
            }
            else
            {
                foreach (var m in LoadedModules)
                {
                    var oidType = m.GetType().Module.GetType($"{m.GetType().Namespace}.OID");
                    var oidName = oidType?.GetEnumName(m.PrimaryActor.OID);
                    if (ImGui.Button($"{m.GetType()} ({m.PrimaryActor.InstanceID:X} '{m.PrimaryActor.Name}' {oidName})"))
                        SetDrawnModule(m);
                }
            }
        }

        private void DrawMovementHints(BossModule.MovementHints? arrows)
        {
            if (arrows == null || arrows.Count == 0 || Camera.Instance == null)
                return;

            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(0, 0));
            ImGuiHelpers.ForceNextWindowMainViewport();
            ImGuiHelpers.SetNextWindowPosRelativeMainViewport(new Vector2(0, 0));
            ImGui.Begin("movement_hints_overlay", ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoBackground);
            ImGui.SetWindowSize(ImGui.GetIO().DisplaySize);

            foreach ((var start, var end, uint color) in arrows)
            {
                DrawWorldLine(start, end, color, Camera.Instance);
                var dir = Vector3.Normalize(end - start);
                var arrowStart = end - 0.4f * dir;
                var offset = 0.07f * Vector3.Normalize(Vector3.Cross(Vector3.UnitY, dir));
                DrawWorldLine(arrowStart + offset, end, color, Camera.Instance);
                DrawWorldLine(arrowStart - offset, end, color, Camera.Instance);
            }

            ImGui.End();
            ImGui.PopStyleVar();
        }

        private void DrawWorldLine(Vector3 start, Vector3 end, uint color, Camera camera)
        {
            var p1 = start.ToSharpDX();
            var p2 = end.ToSharpDX();
            if (!GeometryUtils.ClipLineToNearPlane(ref p1, ref p2, camera.ViewProj))
                return;

            p1 = SharpDX.Vector3.TransformCoordinate(p1, camera.ViewProj);
            p2 = SharpDX.Vector3.TransformCoordinate(p2, camera.ViewProj);
            var p1screen = new Vector2(0.5f * camera.ViewportSize.X * (1 + p1.X), 0.5f * camera.ViewportSize.Y * (1 - p1.Y)) + ImGuiHelpers.MainViewport.Pos;
            var p2screen = new Vector2(0.5f * camera.ViewportSize.X * (1 + p2.X), 0.5f * camera.ViewportSize.Y * (1 - p2.Y)) + ImGuiHelpers.MainViewport.Pos;
            ImGui.GetWindowDrawList().AddLine(p1screen, p2screen, color);
            //ImGui.GetWindowDrawList().AddText(p1screen, color, $"({p1.X:f3},{p1.Y:f3},{p1.Z:f3}) -> ({p2.X:f3},{p2.Y:f3},{p2.Z:f3})");
        }

        private void MainWindowClosed()
        {
            Service.Log("[BMM] Main window closed");
            _mainWindow = null;
        }

        private bool MainWindowClosedByUser()
        {
            if (_drawnModule != null)
            {
                // show module list instead of boss module
                Service.Log("[BMM] Bossmod window closed by user, showing module list instead...");
                SetDrawnModule(null);
                return false;
            }
            else
            {
                // close main window
                Service.Log("[BMM] Bossmod window closed by user, disabling temporarily");
                return true;
            }
        }
    }
}