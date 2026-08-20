// AgOpenWeb
// Copyright (C) 2024-2025 AgOpenWeb Contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see <https://www.gnu.org/licenses/>.

using AgOpenWeb.Models.Track;
using AgOpenWeb.Services.AutoSteer;
using AgOpenWeb.Services.Interfaces;

namespace AgOpenWeb.ViewModels;

/// <summary>
/// MainViewModel partial class containing guidance-related state and event handlers.
/// Guidance computation is done by GpsPipelineService on a background thread.
/// Results are applied via ApplyGpsCycleResult.
/// </summary>
public partial class MainViewModel
{
    #region Guidance State

    // Track guidance state (carried between iterations for track snapping/preview)
    private TrackGuidanceState? _trackGuidanceState;

    #endregion

    #region AutoSteer Event Handlers

    private void OnAutoSteerStateUpdated(object? sender, VehicleStateSnapshot state)
    {
        // AutoSteer fires StateUpdated at the unified control-loop rate
        // (100 Hz, decoupled from GPS — see Plans/Completed/
        // UNIFIED_CONTROL_LOOP_PLAN.md). Latency display can't sit on this
        // path — at 100 Hz the PropertyChanged → TextLayout cascade for
        // the status-bar "Lat: 0.00ms" readout was 5.9% main-thread CPU
        // (Phase 2b trace). We cache the latest value here and let
        // OnStatusTick publish it at 5 Hz.
        //
        // TramControlByte → map service is structural (not display) and
        // stays on the source rate. Single-double / single-byte writes are
        // atomic on x86/ARM — no lock needed.
        _latestGpsToPgnLatencyMs = state.TotalLatencyMs;
        ProcessModuleSwitches(state);
        if (_dispatcher.CheckAccess())
        {
            TramControlByte = state.TramState;
            _mapService.SetTramControlByte(state.TramState);
        }
        else
        {
            _dispatcher.Post(() =>
            {
                TramControlByte = state.TramState;
                _mapService.SetTramControlByte(state.TramState);
            });
        }
    }

    #region Hardware Switches

    private readonly Models.Communication.ModuleSwitchState _moduleSwitchState = new();

    /// <summary>
    /// Feed the hardware work/steer switch bits into the native switch logic
    /// (ModuleCommunicationService.CheckSwitches) and let its events drive the
    /// section master / autosteer toggles. This is the piece that makes the
    /// "Work switch" / "Steer switch" vehicle settings actually do something —
    /// the logic was ported long ago but nothing ever fed it.
    /// </summary>
    private void ProcessModuleSwitches(in VehicleStateSnapshot state)
    {
        // Only with live steer-module data: parser defaults must not flap buttons.
        if (!IsAutoSteerDataOk) return;

        var tool = ConfigStore.Tool;
        var mc = _moduleCommunicationService;
        mc.IsRemoteWorkSystemOn = tool.IsWorkSwitchEnabled || tool.IsSteerSwitchEnabled;
        // Feed the pre-resolved work state (polarity + momentary latch applied)
        // through the native formula: active == (High != ActiveLow), so express
        // WorkSwitchOn as the pin level that makes the formula come out right.
        mc.WorkSwitchHigh = tool.IsWorkSwitchActiveLow ? !state.WorkSwitchOn : state.WorkSwitchOn;
        mc.SteerSwitchHigh = _autoSteerService.LastSteerData.SteerSwitchActive;

        _moduleSwitchState.IsAutoSteerOn = IsAutoSteerEngaged;
        _moduleSwitchState.IsAutoSteerAuto = ConfigStore.AutoSteer.ExternalEnable != 0;
        _moduleSwitchState.AutoButtonState = IsSectionMasterOn
            ? Models.Communication.ButtonStates.Auto : Models.Communication.ButtonStates.Off;
        _moduleSwitchState.ManualButtonState = IsManualSectionMode
            ? Models.Communication.ButtonStates.On : Models.Communication.ButtonStates.Off;
        mc.CheckSwitches(_moduleSwitchState);
    }

    #endregion

    #endregion
}
