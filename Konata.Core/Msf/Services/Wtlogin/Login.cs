﻿using System;
using Konata.Msf;
using Konata.Msf.Packets.Oicq;
using Konata.Msf.Packets.Tlv;

namespace Konata.Msf.Services.Wtlogin
{
    internal class Login : Service
    {
        private Login()
        {
            Register("wtlogin.login", this);
        }

        public static Service Instance { get; } = new Login();

        protected override bool OnRun(Core core, string method, params object[] args)
        {
            switch (method)
            {
                case "Request_TGTGT":
                    return Request_TGTGT(core);
                case "Request_SliderCaptcha":
                    return Request_SliderCaptcha(core, (string)args[0], (string)args[1]);
                case "Request_SmsCaptcha":
                    return Request_SmsCaptcha(core, (string)args[0], (byte[])args[1], (string)args[2], (byte[])args[3]);
                case "Request_RefreshSms":
                    return Request_RefreshSms(core, (string)args[0], (byte[])args[1]);
                default: return false;
            }
        }

        protected override bool OnHandle(Core core, params object[] args)
        {
            if (args == null || args.Length == 0)
                return false;

            var packet = (Packet)args[0];
            var oicqRequest = new OicqRequest(packet.TakeAllBytes(out byte[] _),
                core._keyRing._shareKey);

            Console.WriteLine($"  [oicqRequest] oicqCommand => {oicqRequest._oicqCommand}");
            Console.WriteLine($"  [oicqRequest] oicqVersion => {oicqRequest._oicqVersion}");
            Console.WriteLine($"  [oicqRequest] oicqStatus => {oicqRequest._oicqStatus}");

            core._oicqStatus = oicqRequest._oicqStatus;
            switch (oicqRequest._oicqStatus)
            {
                case OicqStatus.OK:
                    return Handle_WtloginSuccess(core, oicqRequest);

                case OicqStatus.DoVerifySliderCaptcha:
                    return Handle_VerifySliderCaptcha(core, oicqRequest);
                case OicqStatus.DoVerifyDeviceLockViaSms:
                    return Handle_VerifySmsCaptcha(core, oicqRequest);

                case OicqStatus.PreventByIncorrectUserOrPwd:
                    return Handle_InvalidUserOrPassword(core, oicqRequest);
                case OicqStatus.PreventByInvalidEnvironment:
                    return Handle_InvalidEnvironment(core, oicqRequest);
                case OicqStatus.PreventByLoginDenied:
                    return Handle_LoginDenied(core, oicqRequest);

                default: Handle_UnknownOicqRequest(core, oicqRequest); break;
            }

            return false;
        }

        #region Event Requests

        /// <summary>
        /// 請求 OicqRequestTgtgt
        /// </summary>
        /// <param name="core"></param>
        internal bool Request_TGTGT(Core core)
        {
            Console.WriteLine("Submit OicqRequestTGTGT.");

            var sequence = core._ssoMan.GetServiceSequence(name);
            var request = new OicqRequestTgtgt(core._uin, sequence, core._keyRing);

            core._ssoMan.PostMessage(this, request, sequence);

            return true;
        }

        /// <summary>
        /// 請求 OicqRequestCheckImage
        /// </summary>
        /// <param name="core"></param>
        /// <param name="sigSission"></param>
        /// <param name="sigTicket"></param>
        /// <returns></returns>
        internal bool Request_SliderCaptcha(Core core,
            string sigSission, string sigTicket)
        {
            Console.WriteLine("Submit OicqRequestCheckImage.");

            var sequence = core._ssoMan.GetServiceSequence(name);
            var request = new OicqRequestCheckImage(core._uin, core._keyRing,
                sigSission, sigTicket);

            core._ssoMan.PostMessage(this, request, sequence);

            return true;
        }

        /// <summary>
        /// 請求 OicqRequestCheckSms
        /// </summary>
        /// <param name="core"></param>
        /// <param name="sigSession"></param>
        /// <param name="sigSecret"></param>
        /// <param name="sigSmsCode"></param>
        /// <param name="g"></param>
        /// <returns></returns>
        internal bool Request_SmsCaptcha(Core core, string sigSession, byte[] sigSecret,
            string sigSmsCode, byte[] g)
        {
            Console.WriteLine("Submit OicqRequestCheckSms.");

            var sequence = core._ssoMan.GetServiceSequence(name);
            var request = new OicqRequestCheckSms(core._uin, core._keyRing, sigSession,
                sigSecret, sigSmsCode, g);

            core._ssoMan.PostMessage(this, request, sequence);
            return true;
        }

        /// <summary>
        /// 刷新SMS驗證碼. CD 60s
        /// </summary>
        /// <param name="core"></param>
        /// <param name="sigSession"></param>
        /// <param name="sigSecret"></param>
        /// <returns></returns>
        internal bool Request_RefreshSms(Core core, string sigSession, byte[] sigSecret)
        {
            Console.WriteLine("Request send SMS.");

            var sequence = core._ssoMan.GetServiceSequence(name);
            var request = new OicqRequestRefreshSms(core._uin, core._keyRing, sigSession, sigSecret);

            core._ssoMan.PostMessage(this, request, sequence);

            return true;
        }

        #endregion

        #region Event Handlers

        internal bool Handle_VerifySliderCaptcha(Core core, OicqRequest request)
        {
            Console.WriteLine("Do slider verification.");

            var tlvs = request._oicqRequestBody.TakeAllBytes(out var _);
            var unpacker = new TlvUnpacker(tlvs, true);

            Tlv tlv104 = unpacker.TryGetTlv(0x104);
            Tlv tlv192 = unpacker.TryGetTlv(0x192);
            if (tlv104 != null && tlv192 != null)
            {
                var sigSession = ((T104Body)tlv104._tlvBody)._sigSession;
                var sigCaptchaURL = ((T192Body)tlv192._tlvBody)._url;

                core.PostUserEvent(EventType.WtLoginVerifySliderCaptcha, sigSession, sigCaptchaURL,
                    DeviceInfo.Browser.UserAgent);
            }
            return false;
        }

        internal bool Handle_VerifySmsCaptcha(Core core, OicqRequest request)
        {
            Console.WriteLine("Do sms verification.");

            var tlvs = request._oicqRequestBody.TakeAllBytes(out var _);
            var unpacker = new TlvUnpacker(tlvs, true);

            Tlv tlv104 = unpacker.TryGetTlv(0x104);
            Tlv tlv174 = unpacker.TryGetTlv(0x174); // 隨機64字節
            Tlv tlv204 = unpacker.TryGetTlv(0x204); // 賬號申訴
            Tlv tlv178 = unpacker.TryGetTlv(0x178); // 手機號碼
            Tlv tlv17d = unpacker.TryGetTlv(0x17d); // 手機QQ安全中心
            Tlv tlv402 = unpacker.TryGetTlv(0x402);
            Tlv tlv403 = unpacker.TryGetTlv(0x403);
            Tlv tlv17e = unpacker.TryGetTlv(0x17e); // 提示訊息

            if (tlv104 != null && tlv174 != null
                && tlv204 != null && tlv178 != null
                && tlv17d != null && tlv402 != null
                && tlv403 != null && tlv17e != null)
            {
                var sigSession = ((T104Body)tlv104._tlvBody)._sigSession;
                var sigSecret = ((T174Body)tlv174._tlvBody)._sigSecret;
                var sigMessage = ((T17eBody)tlv17e._tlvBody)._message;

                Console.WriteLine($"[Hint] {sigMessage}");

                core.PostSystemEvent(EventType.WtLoginSendSms, sigSession, sigSecret);
            }

            return false;
        }

        internal bool Handle_VerifyImageCaptcha(Core core, OicqRequest request)
        {
            Console.WriteLine("Do image verification.");

            // core.PostUserEvent(EventType.WtLoginVerifyImageCaptcha);

            return false;
        }

        internal bool Handle_VerifyDeviceLock(Core core, OicqRequest request)
        {
            Console.WriteLine("Do DeviceLock verification.");
            core.PostSystemEvent(EventType.LoginFailed);
            return false;
        }

        internal bool Handle_WtloginSuccess(Core core, OicqRequest request)
        {
            Console.WriteLine("Wtlogin success.");

            core._ssoMan.DestroyServiceSequence(name);
            core.PostSystemEvent(EventType.WtLoginOK);

            return false;
        }

        internal bool Handle_InvalidUserOrPassword(Core core, OicqRequest request)
        {
            Console.WriteLine("[Error] Incorrect account or password.");
            core.PostSystemEvent(EventType.LoginFailed);
            return false;
        }

        internal bool Handle_InvalidEnvironment(Core core, OicqRequest request)
        {
            Console.WriteLine("[Error] Invalid login environment.");

            var tlvs = request._oicqRequestBody.TakeAllBytes(out var _);
            var unpacker = new TlvUnpacker(tlvs, true);

            Tlv tlv146 = unpacker.TryGetTlv(0x146);
            if (tlv146 != null)
            {
                var errorTitle = ((T146Body)tlv146._tlvBody)._title;
                var errorMessage = ((T146Body)tlv146._tlvBody)._message;

                Console.WriteLine($"[Error] {errorTitle} {errorMessage}");
            }

            core.PostSystemEvent(EventType.LoginFailed);
            return false;
        }

        internal bool Handle_LoginDenied(Core core, OicqRequest request)
        {
            Console.WriteLine("[Error] Login denied.");

            var tlvs = request._oicqRequestBody.TakeAllBytes(out var _);
            var unpacker = new TlvUnpacker(tlvs, true);

            Tlv tlv146 = unpacker.TryGetTlv(0x146);
            if (tlv146 != null)
            {
                var errorTitle = ((T146Body)tlv146._tlvBody)._title;
                var errorMessage = ((T146Body)tlv146._tlvBody)._message;

                Console.WriteLine($"[Error] {errorTitle} {errorMessage}");
            }

            core.PostSystemEvent(EventType.LoginFailed);
            return false;
        }

        internal bool Handle_UnknownOicqRequest(Core core, OicqRequest request)
        {
            Console.WriteLine("[Error] Unknown OicqRequest received.");
            core.PostSystemEvent(EventType.LoginFailed);
            return false;
        }

        #endregion
    }
}
