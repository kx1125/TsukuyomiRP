// Copyright (c) 2024 Nico de Poel
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.

using System;
using UnityEngine;

namespace Tsukuyomi.Rendering.FSR3
{
    [Serializable]
    public sealed class Fsr3UpscalerShaders
    {
        public ComputeShader prepareInputsPass;
        public ComputeShader lumaPyramidPass;
        public ComputeShader shadingChangePyramidPass;
        public ComputeShader shadingChangePass;
        public ComputeShader prepareReactivityPass;
        public ComputeShader lumaInstabilityPass;
        public ComputeShader accumulatePass;
        public ComputeShader sharpenPass;
        public ComputeShader autoGenReactivePass;
        public ComputeShader tcrAutoGenPass;
        public ComputeShader debugViewPass;
    }
}

