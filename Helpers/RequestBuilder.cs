using Google.Protobuf;
using PokemonGo.RocketAPI.Enums;
using POGOProtos.Networking;
using POGOProtos.Networking.Envelopes;
using POGOProtos.Networking.Requests;
using POGOProtos.Networking.Platform;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using PokemonGo.RocketAPI.Extensions;
using POGOProtos.Networking.Platform.Requests;

namespace PokemonGo.RocketAPI.Helpers
{
    public class RequestBuilder
    {
        private static readonly Stopwatch _internalWatch = new Stopwatch();

        public static double currentSpeed { get; set; }
        private readonly string _authToken;
        private readonly AuthType _authType;
        private readonly double _latitude;
        private readonly double _longitude;
        private readonly double _accuracy = (float)Math.Round(GenRandom(50, 100), 3);
        private readonly double _altitude;
        private readonly double _currentSpeed;
        private readonly AuthTicket _authTicket;
        private int _startTime;
        private ulong _nextRequestId;
        private readonly ISettings settings;
        private ISettings DeviceBrand { get; }

        private ByteString SessionHash
        {
            get { return settings.SessionHash; }
            set { settings.SessionHash = value; }
        }

        public void GenerateNewHash()
        {
            var hashBytes = new byte[16];
            RandomDevice.NextBytes(hashBytes);
            SessionHash = ByteString.CopyFrom(hashBytes);
        }
        public RequestBuilder(string authToken, AuthType authType, double latitude, double longitude, double altitude, double horizontalAccuracy, ISettings settings, AuthTicket authTicket = null)
        {
            _authToken = authToken;
            _authType = authType;
            _currentSpeed = currentSpeed;
            _latitude = latitude;
            _longitude = longitude;
            _altitude = altitude;
            _authTicket = authTicket;
            _accuracy = horizontalAccuracy;
            this.settings = settings;
            _nextRequestId = Convert.ToUInt64(RandomDevice.NextDouble() * Math.Pow(10, 18));
            if (_startTime == 0)
                _startTime = Utils.GetTime(true);
            if (SessionHash == null)
            {
                GenerateNewHash();
            }
        }  

        private RequestEnvelope.Types.PlatformRequest GenerateSignature(IEnumerable<IMessage> requests)
        {
            var ticketBytes = _authTicket.ToByteArray();
            var sig = new SignalAgglomUpdates()
            {
                EpochTimestampMs = (ulong)Utils.GetTime(true),
                TimestampMsSinceStart = (ulong)(Utils.GetTime(true) - _startTime),
                LocationHashByTokenSeed = (uint)Utils.GenerateLocation1(ticketBytes, _latitude, _longitude, _accuracy),
                LocationHash = (uint)Utils.GenerateLocation2(_latitude, _longitude, _accuracy),
                SensorUpdates = new SignalAgglomUpdates.Types.SensorUpdate()
                {
                    GravityX = GenRandom(-0.31110161542892456, 0.1681540310382843),
                    GravityY = GenRandom(-0.6574847102165222, -0.07290205359458923),
                    GravityZ = GenRandom(-0.9943905472755432, -0.7463029026985168),
                    Timestamp = (ulong)(Utils.GetTime(true) - _startTime - RandomDevice.Next(100, 400)),
                    AccelerationX = GenRandom(-0.139084026217, 0.138112977147),
                    AccelerationY = GenRandom(-0.2, 0.19),
                    AccelerationZ = GenRandom(-0.2, 0.4),
                    MagneticFieldX = GenRandom(-47.149471283, 61.8397789001),
                    MagneticFieldY = GenRandom(-47.149471283, 61.8397789001),
                    MagneticFieldZ = GenRandom(-47.149471283, 5),
                    AttitudePitch = GenRandom(0.0729667818829, 0.0729667818829),
                    AttitudeYaw = GenRandom(-2.788630499244109, 3.0586791383810468),
                    AttitudeRoll = GenRandom(-0.34825887123552773, 0.19347580173737935),
                    RotationRateX = GenRandom(-0.9703824520111084, 0.8556089401245117),
                    RotationRateY = GenRandom(-1.7470258474349976, 1.4218578338623047),
                    RotationRateZ = GenRandom(-0.9681901931762695, 0.8396636843681335),
                    Status = 3
                },
                DeviceInfo = new SignalAgglomUpdates.Types.DeviceInfo()
                {
                    DeviceId = settings.DeviceId,
                    AndroidBoardName = settings.AndroidBoardName,
                    AndroidBootloader = settings.AndroidBootloader,
                    DeviceBrand = settings.DeviceBrand,
                    DeviceModel = settings.DeviceModel,
                    DeviceModelIdentifier = settings.DeviceModelIdentifier,
                    DeviceCommsModel = settings.DeviceModelBoot,
                    HardwareManufacturer = settings.HardwareManufacturer,
                    HardwareModel = settings.HardwareModel,
                    FirmwareBrand = settings.FirmwareBrand,
                    FirmwareTags = settings.FirmwareTags,
                    FirmwareType = settings.FirmwareType,
                    FirmwareFingerprint = settings.FirmwareFingerprint
                }
                };
            if (settings.DeviceBrand == "APPLE")
                sig.LocationUpdates.Add(new SignalAgglomUpdates.Types.LocationUpdate()
                {
                    Name = "fused",
                    TimestampMs = (Utils.GetTime(true) - _startTime - RandomDevice.Next(100, 300)),
                    Latitude = (float)_latitude,
                    Longitude = (float)_longitude,
                    Altitude = (-(float)_altitude) - 13,
                    DeviceSpeed = (float)_currentSpeed,
                    DeviceCourse = -1,
                    HorizontalAccuracy = (float)_accuracy,
                    VerticalAccuracy = RandomDevice.Next(2, 5),
                    ProviderStatus = 3,
                    Floor = 0,
                    LocationType = 1
                });
            else
                sig.LocationUpdates.Add(new SignalAgglomUpdates.Types.LocationUpdate()
                {
                    Name = "fused",
                    TimestampMs = (Utils.GetTime(true) - _startTime - RandomDevice.Next(100, 300)),
                    Latitude = (float)_latitude,
                    Longitude = (float)_longitude,
                    Altitude = (-(float)_altitude) - 13,
                    HorizontalAccuracy = (float)_accuracy,
                    VerticalAccuracy = RandomDevice.Next(2, 5),
                    ProviderStatus = 3,
                    Floor = 0,
                    LocationType = 1
                });

            //Compute 10
            var x = new System.Data.HashFunction.xxHash(32, 0x1B845238);
            var firstHash = BitConverter.ToUInt32(x.ComputeHash(_authTicket.ToByteArray()), 0);
            x = new System.Data.HashFunction.xxHash(32, firstHash);
            var locationBytes = BitConverter.GetBytes(_latitude).Reverse()
                .Concat(BitConverter.GetBytes(_longitude).Reverse())
                .Concat(BitConverter.GetBytes(_accuracy).Reverse()).ToArray();
            sig.LocationHashByTokenSeed = BitConverter.ToUInt32(x.ComputeHash(locationBytes), 0);
            //Compute 20
            x = new System.Data.HashFunction.xxHash(32, 0x1B845238);
            sig.LocationHash = BitConverter.ToUInt32(x.ComputeHash(locationBytes), 0);
            //Compute 24
            x = new System.Data.HashFunction.xxHash(64, 0x1B845238);
            var seed = BitConverter.ToUInt64(x.ComputeHash(_authTicket.ToByteArray()), 0);
            x = new System.Data.HashFunction.xxHash(64, seed);
            foreach (var req in requests)
                sig.RequestHashes.Add(BitConverter.ToUInt64(x.ComputeHash(req.ToByteArray()), 0));

            sig.Field22 = SessionHash;
            //static for now
            sig.Field25 = 7363665268261373700;




            RequestEnvelope.Types.PlatformRequest val = new RequestEnvelope.Types.PlatformRequest();
            val.Type = PlatformRequestType.SendEncryptedSignature;
            SendEncryptedSignatureRequest sigRequest = new SendEncryptedSignatureRequest();
            sigRequest.EncryptedSignature = ByteString.CopyFrom(Encrypt(sig.ToByteArray()));
            val.RequestMessage = sigRequest.ToByteString();
            return val;
            
        }
        private byte[] Encrypt(byte[] bytes)
        {
            var outputLength = 32 + bytes.Length + (256 - (bytes.Length % 256));
            var ptr = Marshal.AllocHGlobal(outputLength);
            var ptrOutput = Marshal.AllocHGlobal(outputLength);
            FillMemory(ptr, (uint)outputLength, 0);
            FillMemory(ptrOutput, (uint)outputLength, 0);
            Marshal.Copy(bytes, 0, ptr, bytes.Length);
            try
            {
                int outputSize = outputLength;
                EncryptNative(ptr, bytes.Length, new byte[32], 32, ptrOutput, out outputSize);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            var output = new byte[outputLength];
            Marshal.Copy(ptrOutput, output, 0, outputLength);
            Marshal.FreeHGlobal(ptr);
            Marshal.FreeHGlobal(ptrOutput);
            return output;
        }

        [DllImport("Resources/encrypt.dll", EntryPoint = "encrypt", CharSet = CharSet.Auto, CallingConvention = CallingConvention.Cdecl)]
        static extern private void EncryptNative(IntPtr arr, int length, byte[] iv, int ivsize, IntPtr output, out int outputSize);
        [DllImport("kernel32.dll", EntryPoint = "RtlFillMemory", SetLastError = false)]
        static extern void FillMemory(IntPtr destination, uint length, byte fill);

        public RequestEnvelope GetRequestEnvelope(params Request[] customRequests)
        {
            var e = new RequestEnvelope
            {
                StatusCode = 2, //1

                RequestId = _nextRequestId++, //3
                Requests = { customRequests }, //4

                //Unknown6 = , //6
                Latitude = _latitude, //7
                Longitude = _longitude, //8
                Accuracy = _accuracy, //9
                AuthTicket = _authTicket, //11
                MsSinceLastLocationfix = RandomDevice.Next(800, 1900) //12
            };
            e.PlatformRequests.Add(GenerateSignature(customRequests));
            return e;
        }


        public RequestEnvelope GetInitialRequestEnvelope(params Request[] customRequests)
        {
            var e = new RequestEnvelope
            {
                StatusCode = 2, //1

                RequestId = _nextRequestId++, //3
                Requests = { customRequests }, //4
                
                //Unknown6 = , //6
                Latitude = _latitude, //7
                Longitude = _longitude, //8
                Accuracy = _accuracy, //9
                AuthInfo = new POGOProtos.Networking.Envelopes.RequestEnvelope.Types.AuthInfo
                {
                    Provider = _authType == AuthType.Google ? "google" : "ptc",
                    Token = new POGOProtos.Networking.Envelopes.RequestEnvelope.Types.AuthInfo.Types.JWT
                    {
                        Contents = _authToken,
                        Unknown2 = 59
                    }
                }, //10
                MsSinceLastLocationfix = RandomDevice.Next(1200, 1900) //12
            };
            return e;
        }



        public RequestEnvelope GetRequestEnvelope(RequestType type, IMessage message)
        {
            return GetRequestEnvelope(new Request()
            {
                RequestType = type,
                RequestMessage = message.ToByteString()
            });

        }

        private static readonly Random RandomDevice = new Random();

        public static double GenRandom(double num)
        {
                var randomFactor = 0.3f;
                var randomMin = (num * (1 - randomFactor));
                var randomMax = (num * (1 + randomFactor));
                var randomizedDelay = RandomDevice.NextDouble() * (randomMax - randomMin) + randomMin; ;
                return randomizedDelay;;
        }

        public static double GenRandom(double min, double max)
        {
            return RandomDevice.NextDouble() * (max - min) + min;
        }
    }
=======
﻿using Google.Protobuf;
using PokemonGo.RocketAPI.Enums;
using POGOProtos.Networking;
using POGOProtos.Networking.Envelopes;
using POGOProtos.Networking.Requests;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using PokemonGo.RocketAPI.Extensions;

namespace PokemonGo.RocketAPI.Helpers
{
    public class RequestBuilder
    {
        public static double currentSpeed { get; set; }
        private readonly string _authToken;
        private readonly AuthType _authType;
        private readonly double _latitude;
        private readonly double _longitude;
        private readonly double _accuracy = (float)Math.Round(GenRandom(50, 250), 7);
        private readonly double _altitude;
        private readonly double _currentSpeed;
        private readonly AuthTicket _authTicket;
        private int _startTime;
        private ulong _nextRequestId;
        private readonly ISettings settings;

        public RequestBuilder(string authToken, AuthType authType, double latitude, double longitude, double altitude, double horizontalAccuracy, ISettings settings, AuthTicket authTicket = null)
        {
            _authToken = authToken;
            _authType = authType;
            _currentSpeed = currentSpeed;
            _latitude = latitude;
            _longitude = longitude;
            _altitude = altitude;
            _authTicket = authTicket;
            _accuracy = horizontalAccuracy;
            this.settings = settings;
            _nextRequestId = Convert.ToUInt64(RandomDevice.NextDouble() * Math.Pow(10, 18));
            if (_startTime == 0)
                _startTime = Utils.GetTime(true);
        }

        private Unknown6 GenerateSignature(IEnumerable<IMessage> requests)
        {
            var ticketBytes = _authTicket.ToByteArray();
            var sig = new Signature()
            {
                Timestamp = (ulong)Utils.GetTime(true),
                TimestampSinceStart = (ulong)(Utils.GetTime(true) - _startTime),
                LocationHash1 = Utils.GenerateLocation1(ticketBytes, _latitude, _longitude, _altitude),
                LocationHash2 = Utils.GenerateLocation2(_latitude, _longitude, _altitude),
                SensorInfo = new Signature.Types.SensorInfo()
                {
                    AccelNormalizedX = GenRandom(-0.31110161542892456, 0.1681540310382843),
                    AccelNormalizedY = GenRandom(-0.6574847102165222, -0.07290205359458923),
                    AccelNormalizedZ = GenRandom(-0.9943905472755432, -0.7463029026985168),
                    TimestampSnapshot = (ulong)(Utils.GetTime(true) - _startTime - RandomDevice.Next(100, 400)),
                    MagnetometerX = GenRandom(-0.139084026217, 0.138112977147),
                    MagnetometerY = GenRandom(-0.2, 0.19),
                    MagnetometerZ = GenRandom(-0.2, 0.4),
                    AngleNormalizedX = GenRandom(-47.149471283, 61.8397789001),
                    AngleNormalizedY = GenRandom(-47.149471283, 61.8397789001),
                    AngleNormalizedZ = GenRandom(-47.149471283, 5),
                    AccelRawX = GenRandom(0.0729667818829, 0.0729667818829),
                    AccelRawY = GenRandom(-2.788630499244109, 3.0586791383810468),
                    AccelRawZ = GenRandom(-0.34825887123552773, 0.19347580173737935),
                    GyroscopeRawX = GenRandom(-0.9703824520111084, 0.8556089401245117),
                    GyroscopeRawY = GenRandom(-1.7470258474349976, 1.4218578338623047),
                    GyroscopeRawZ = GenRandom(-0.9681901931762695, 0.8396636843681335),
                    AccelerometerAxes = 3
                },
                DeviceInfo = new Signature.Types.DeviceInfo()
                {
                    DeviceId = settings.DeviceId,
                    AndroidBoardName = settings.AndroidBoardName,
                    AndroidBootloader = settings.AndroidBootloader,
                    DeviceBrand = settings.DeviceBrand,
                    DeviceModel = settings.DeviceModel,
                    DeviceModelIdentifier = settings.DeviceModelIdentifier,
                    DeviceModelBoot = settings.DeviceModelBoot,
                    HardwareManufacturer = settings.HardwareManufacturer,
                    HardwareModel = settings.HardwareModel,
                    FirmwareBrand = settings.FirmwareBrand,
                    FirmwareTags = settings.FirmwareTags,
                    FirmwareType = settings.FirmwareType,
                    FirmwareFingerprint = settings.FirmwareFingerprint
                }
            };
            sig.LocationFix.Add(new POGOProtos.Networking.Envelopes.Signature.Types.LocationFix()
            {
                Provider = "fused",
                TimestampSnapshot = (ulong)(Utils.GetTime(true) - _startTime - RandomDevice.Next(100, 300)),
                Latitude = (float)_latitude,
                Longitude = (float)_longitude,
                Altitude = (float)_altitude,
                Speed = (float)_currentSpeed,
                Course = -1,
                HorizontalAccuracy = (float)_accuracy,
                VerticalAccuracy = RandomDevice.Next(2, 5),
                ProviderStatus = 3,
                Floor = 0,
                LocationType = 1
            });

            //Compute 10
            var x = new System.Data.HashFunction.xxHash(32, 0x1B845238);
            var firstHash = BitConverter.ToUInt32(x.ComputeHash(_authTicket.ToByteArray()), 0);
            x = new System.Data.HashFunction.xxHash(32, firstHash);
            var locationBytes = BitConverter.GetBytes(_latitude).Reverse()
                .Concat(BitConverter.GetBytes(_longitude).Reverse())
                .Concat(BitConverter.GetBytes(_accuracy).Reverse()).ToArray();
            sig.LocationHash1 = BitConverter.ToUInt32(x.ComputeHash(locationBytes), 0);
            //Compute 20
            x = new System.Data.HashFunction.xxHash(32, 0x1B845238);
            sig.LocationHash2 = BitConverter.ToUInt32(x.ComputeHash(locationBytes), 0);
            //Compute 24
            x = new System.Data.HashFunction.xxHash(64, 0x1B845238);
            var seed = BitConverter.ToUInt64(x.ComputeHash(_authTicket.ToByteArray()), 0);
            x = new System.Data.HashFunction.xxHash(64, seed);
            foreach (var req in requests)
                sig.RequestHash.Add(BitConverter.ToUInt64(x.ComputeHash(req.ToByteArray()), 0));

            //static for now
            sig.SessionHash = ByteString.CopyFrom(new byte[16] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F });
            sig.Unknown25 = 7363665268261373700;

            Unknown6 val = new Unknown6();
            val.RequestType = 6;
            val.Unknown2 = new Unknown6.Types.Unknown2();
            val.Unknown2.EncryptedSignature = ByteString.CopyFrom(Encrypt(sig.ToByteArray()));
            return val;
        }
        private byte[] Encrypt(byte[] bytes)
        {
            var outputLength = 32 + bytes.Length + (256 - (bytes.Length % 256));
            var ptr = Marshal.AllocHGlobal(outputLength);
            var ptrOutput = Marshal.AllocHGlobal(outputLength);
            FillMemory(ptr, (uint)outputLength, 0);
            FillMemory(ptrOutput, (uint)outputLength, 0);
            Marshal.Copy(bytes, 0, ptr, bytes.Length);
            try
            {
                int outputSize = outputLength;
                EncryptNative(ptr, bytes.Length, new byte[32], 32, ptrOutput, out outputSize);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            var output = new byte[outputLength];
            Marshal.Copy(ptrOutput, output, 0, outputLength);
            Marshal.FreeHGlobal(ptr);
            Marshal.FreeHGlobal(ptrOutput);
            return output;
        }

        [DllImport("Resources/encrypt.dll", EntryPoint = "encrypt", CharSet = CharSet.Auto, CallingConvention = CallingConvention.Cdecl)]
        static extern private void EncryptNative(IntPtr arr, int length, byte[] iv, int ivsize, IntPtr output, out int outputSize);
        [DllImport("kernel32.dll", EntryPoint = "RtlFillMemory", SetLastError = false)]
        static extern void FillMemory(IntPtr destination, uint length, byte fill);

        public RequestEnvelope GetRequestEnvelope(params Request[] customRequests)
        {
            var e = new RequestEnvelope
            {
                StatusCode = 2, //1

                RequestId = _nextRequestId++, //3
                Requests = { customRequests }, //4

                //Unknown6 = , //6
                Latitude = _latitude, //7
                Longitude = _longitude, //8
                Accuracy = _accuracy, //9
                AuthTicket = _authTicket, //11
                MsSinceLastLocationfix = 989 //12
            };
            e.Unknown6.Add(GenerateSignature(customRequests));
            return e;
        }


        public RequestEnvelope GetInitialRequestEnvelope(params Request[] customRequests)
        {
            var e = new RequestEnvelope
            {
                StatusCode = 2, //1

                RequestId = _nextRequestId++, //3
                Requests = { customRequests }, //4

                //Unknown6 = , //6
                Latitude = _latitude, //7
                Longitude = _longitude, //8
                Accuracy = _accuracy, //9
                AuthInfo = new POGOProtos.Networking.Envelopes.RequestEnvelope.Types.AuthInfo
                {
                    Provider = _authType == AuthType.Google ? "google" : "ptc",
                    Token = new POGOProtos.Networking.Envelopes.RequestEnvelope.Types.AuthInfo.Types.JWT
                    {
                        Contents = _authToken,
                        Unknown2 = 14
                    }
                }, //10
                MsSinceLastLocationfix = 989 //12
            };
            return e;
        }



        public RequestEnvelope GetRequestEnvelope(RequestType type, IMessage message)
        {
            return GetRequestEnvelope(new Request()
            {
                RequestType = type,
                RequestMessage = message.ToByteString()
            });

        }

        private static readonly Random RandomDevice = new Random();

        public static double GenRandom(double num)
        {
            var randomFactor = 0.3f;
            var randomMin = (num * (1 - randomFactor));
            var randomMax = (num * (1 + randomFactor));
            var randomizedDelay = RandomDevice.NextDouble() * (randomMax - randomMin) + randomMin; ;
            return randomizedDelay; ;
        }

        public static double GenRandom(double min, double max)
        {
            return RandomDevice.NextDouble() * (max - min) + min;
        }
    }
}
