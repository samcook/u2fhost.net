using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using u2flib;
using u2flib.Data.Messages;
using u2flib.Util;

namespace u2fhost
{
	public class U2Fv2
	{
		public static async Task<RegisterResponse> RegisterAsync(U2FHidDevice u2fHidDevice, StartedRegistration request, string facet)
		{
			ValidateRequest(request, facet);

			var appParam = GetApplicationParameter(request.AppId);

			var clientData = GetRegistrationClientData(request.Challenge, facet);
			var challengeParam = GetChallengeParameter(clientData);

			var data = challengeParam.Concat(appParam).ToArray();
			var p1 = (byte)0x03;
			var p2 = (byte)0x00;

			var response = await u2fHidDevice.SendApduAsync(Constants.INS_ENROLL, p1, p2, data);

			var registrationDataBase64 = Utils.ByteArrayToBase64String(response);
			var clientDataBase64 = Utils.ByteArrayToBase64String(Encoding.ASCII.GetBytes(clientData));

			var registerResponse = new RegisterResponse(registrationDataBase64, clientDataBase64);

			return registerResponse;
		}

		public static byte[] GetApplicationParameter(string appId)
		{
			var sha256 = new SHA256Managed();
			return sha256.ComputeHash(Encoding.ASCII.GetBytes(appId));
		}

		public static string GetRegistrationClientData(string challenge, string facet)
		{
			var clientData = new
			{
				typ = "navigator.id.finishEnrollment",
				challenge = challenge,
				origin = facet
			};

			return JsonConvert.SerializeObject(clientData);
		}

		public static byte[] GetChallengeParameter(string clientData)
		{
			var sha256 = new SHA256Managed();
			return sha256.ComputeHash(Encoding.ASCII.GetBytes(clientData));
		}

		public static async Task<AuthenticateResponse> AuthenticateAsync(U2FHidDevice u2fHidDevice, StartedAuthentication request, string facet, bool checkOnly)
		{
			ValidateRequest(request, facet);

			var sha256 = new SHA256Managed();
			var appParam = sha256.ComputeHash(Encoding.ASCII.GetBytes(request.AppId));

			var clientDataString = GetAuthenticationClientData(request.Challenge, facet);
			var clientParam = sha256.ComputeHash(Encoding.ASCII.GetBytes(clientDataString));

			var keyHandleDecoded = Utils.Base64StringToByteArray(request.KeyHandle);

			var byteArrayBuilder = new ByteArrayBuilder();
			byteArrayBuilder.Append(clientParam);
			byteArrayBuilder.Append(appParam);
			byteArrayBuilder.Append((byte)keyHandleDecoded.Length);
			byteArrayBuilder.Append(keyHandleDecoded);

			var data = byteArrayBuilder.GetBytes();
			var p1 = (byte)(checkOnly ? 0x07 : 0x03);
			var p2 = (byte)0x00;

			var response = await u2fHidDevice.SendApduAsync(Constants.INS_SIGN, p1, p2, data);

			var responseBase64 = Utils.ByteArrayToBase64String(response);
			var clientDataBase64 = Utils.ByteArrayToBase64String(Encoding.ASCII.GetBytes(clientDataString));

			var authenticateResponse = new AuthenticateResponse(clientDataBase64, responseBase64, request.KeyHandle);

			return authenticateResponse;
		}

		private static string GetAuthenticationClientData(string challenge, string facet)
		{
			var clientData = new
			{
				typ = "navigator.id.getAssertion",
				challenge = challenge,
				origin = facet
			};

			return JsonConvert.SerializeObject(clientData);
		}

		private static void ValidateRequest(StartedRegistration request, string facet)
		{
			if (request.Version != U2F.U2FVersion)
			{
				throw new Exception($"Unsupported U2F version: {request.Version}");
			}
		}

		private static void ValidateRequest(StartedAuthentication request, string facet)
		{
			if (request.Version != U2F.U2FVersion)
			{
				throw new Exception($"Unsupported U2F version: {request.Version}");
			}
		}
	}
}