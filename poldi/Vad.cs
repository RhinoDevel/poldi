
//    P.O.L.D.I. - Private Offline Local Digital Intelligence
//    Copyright (C) 2026  Marcel Timm, RhinoDevel
//
//    This program is free software: you can redistribute it and/or modify
//    it under the terms of the GNU General Public License as published by
//    the Free Software Foundation, either version 3 of the License, or
//    (at your option) any later version.
//
//    This program is distributed in the hope that it will be useful,
//    but WITHOUT ANY WARRANTY; without even the implied warranty of
//    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//    GNU General Public License for more details.
//
//    You should have received a copy of the GNU General Public License
//    along with this program.  If not, see <https://www.gnu.org/licenses/>.

// Marcel Timm, RhinoDevel, 2026jul26

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace Poldi
{
    /// <summary>
    /// Singleton for voice activity detection.
    /// </summary>
    internal static class Vad
    {
        /// <remarks>
        /// E.g. for 5 => 5 x 32ms = 160ms.
        /// </remarks>
        private static readonly int _minConsecutivePositives =
            C.D<int>("vad:minConsecutivePositives");
        private static readonly float _threshold =
            C.D<float>("vad:threshold");

        private const int _samplingRate = 16000; // Hz
        private const int _windowSizeSample = 512; // <=> 32ms
        private const int _contextSize = 64;

        private sealed class Data : IDisposable
        {
            internal const int StateDimOne = 2;
            internal const int StateDimTwo = 1;
            internal const int StateDimThree = 128;
            internal readonly InferenceSession Session;
            internal readonly float[] Input;
            private readonly DenseTensor<float> _inputTensor;
            internal readonly float[] State;
            private readonly DenseTensor<float> _stateTensor;
            internal readonly float[] Context;
            private readonly DenseTensor<long> _srTensor;

            internal readonly IReadOnlyList<NamedOnnxValue> Inputs;

            internal Data(string modelFilePath)
            {
                Debug.Assert(0 < modelFilePath.Trim().Length);

                Session = new InferenceSession(
                    modelFilePath,
                    new SessionOptions
                    {
                        InterOpNumThreads = 1,
                        IntraOpNumThreads = 1,
                        EnableCpuMemArena = true
                    });

                Input = new float[_contextSize + _windowSizeSample];
                _inputTensor = new DenseTensor<float>(Input, [1, Input.Length]);

                State = new float[StateDimOne * StateDimTwo * StateDimThree];
                _stateTensor = new DenseTensor<float>(
                    State, [StateDimOne, StateDimTwo, StateDimThree]);

                Context = new float[_contextSize];

                _srTensor = new DenseTensor<long>(
                    new[] { (long)_samplingRate }, [1]);

                Inputs = new List<NamedOnnxValue>
                {
                    NamedOnnxValue.CreateFromTensor("input", _inputTensor),
                    NamedOnnxValue.CreateFromTensor("sr", _srTensor),
                    NamedOnnxValue.CreateFromTensor("state", _stateTensor)
                };
            }

            public void Dispose() => Session.Dispose();
        }

        private static float GetProb(Data data, float[] samples)
        {
            Debug.Assert(samples.Length == _windowSizeSample);

            Tensor<float> output;
            DenseTensor<float> stateNew;

            Array.Copy(data.Context, 0, data.Input, 0, _contextSize);
            Array.Copy(samples, 0, data.Input, _contextSize, _windowSizeSample);

            using var outputs = data.Session.Run(data.Inputs);

            Debug.Assert(outputs[0].Name == "output");
            output = outputs[0].AsTensor<float>();
            Debug.Assert(outputs[1].Name == "stateN");
            stateNew = (DenseTensor<float>)outputs[1].AsTensor<float>();

            Array.Copy(
                data.Input, _windowSizeSample, data.Context, 0, _contextSize);

            Debug.Assert(stateNew.Dimensions[0] == Data.StateDimOne);
            Debug.Assert(stateNew.Dimensions[1] == Data.StateDimTwo);
            Debug.Assert(stateNew.Dimensions[2] == Data.StateDimThree);
            stateNew.Buffer.Span.CopyTo(data.State);
            
            return output[0];
        }

        internal static bool HasVoiceActivity(
            string modelFilePath, float[] allSamples)
        {
            using var s = new Data(modelFilePath);
            float[] samples = new float[_windowSizeSample];
            int consecutivePositives = 0;

            for(int i = 0; i < allSamples.Length; i += _windowSizeSample)
            {
                // _windowsSizeSample == 512
                //
                // allSamples.Length  == 100
                // => padding          = 512 - 100 +   0 = 412
                //
                // allSamples.Length  == 612
                // => padding          = 512 - 612 + 512 = 412

                // allSamples.Length  == 1024

                int padding = _windowSizeSample - allSamples.Length + i;
                
                int j = -1;
                float voiceProb;

                if(padding < 0)
                {
                    padding = 0;
                }

                while(++j < _windowSizeSample - padding)
                {
                    samples[j] = allSamples[i + j];
                }

                for(; j < _windowSizeSample; ++j)
                {
                    samples[j] = 0.0f;
                }

                voiceProb = GetProb(s, samples);
                if(_threshold <= voiceProb)
                {
                    ++consecutivePositives;
                    if(consecutivePositives == _minConsecutivePositives)
                    {
                        return true;
                    }
                }
                else
                {
                    consecutivePositives = 0;
                }
            }            
            return false;
        }
    }
}
