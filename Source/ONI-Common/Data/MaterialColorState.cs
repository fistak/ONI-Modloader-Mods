﻿using System.Collections.Generic;

namespace ONI_Common.Data
{
    public class MaterialColorState
    {
        private float _gasPressureEnd = 2.5f;

        public ColorMode ColorMode { get; set; } = ColorMode.Json;

        public bool Enabled { get; set; } = true;

        public float GasPressureEnd
        {
            get
            {
                return this._gasPressureEnd;
            }

            set
            {
                this._gasPressureEnd = this._gasPressureEnd <= 0 ? float.Epsilon : value;
            }
        }

        // not used anymore
        public float GasPressureStart { get; set; } = 0.1f;

        public bool LegacyTileColorHandling { get; set; } = false;

        // gas overlay
        public float MinimumGasColorIntensity { get; set; } = 0.25f;

        // major fps drop when enabled
        public bool AdvancedGasOverlayDebugging { get; set; } = false;

        public bool ShowEarDrumPopMarker { get; set; } = true;

        public bool ShowBuildingsAsWhite { get; set; }

        public bool ShowMissingElementColorInfos { get; set; }

        public bool ShowMissingTypeColorOffsets { get; set; }

        public string[] TileNames { get; set; }
    }
}