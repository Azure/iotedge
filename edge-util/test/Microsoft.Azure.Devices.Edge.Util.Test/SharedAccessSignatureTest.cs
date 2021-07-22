// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Test
{
    using System;
    using System.Net;
    using System.Threading;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using Xunit;

    public class SharedAccessKeySignatureTest
    {
        const string IOT_HUB_NAME = "TestHub";
        const string MISSING_SHAREDACCESSSIGNATURE_RAW_TOKEN = "\nsig=test&se=0&sr=test";
        const string EXPIRED_RAW_TOKEN = "SharedAccessSignature\nsig=test&se=0&sr=test";
        const string BAD_TOKEN = "test";
        const string BAD_TOKEN_MISSING_SIGNATURE = "SharedAccessSignature\nse=0&sr=test";
        const string BAD_TOKEN_MISSING_EXPIRY = "SharedAccessSignature\nsig=test&sr=test";
        const string BAD_TOKEN_MISSING_ENCODING_AUDIENCE = "SharedAccessSignature\nsig=test&se=0;";
        const string WHITE_SPACE = "  ";

        [Fact]
        [Unit]
        public void IsExpiredTest()
        {
            var sas = SharedAccessSignature.Parse(IOT_HUB_NAME, this.GetAlmostExpiredToken());

            Thread.Sleep(5 * 1000);

            Assert.True(sas.IsExpired());

            sas = SharedAccessSignature.Parse(IOT_HUB_NAME, this.GetNonExpiredToken());

            Assert.False(sas.IsExpired());
        }

        [Fact]
        [Unit]
        public void ParseThrowsWithNullIotHubNameTest() => Assert.Throws<ArgumentNullException>(() => SharedAccessSignature.Parse(null, EXPIRED_RAW_TOKEN));

        [Fact]
        [Unit]
        public void ParseThrowsWithEmptyIotHubNameTest() => Assert.Throws<ArgumentNullException>(() => SharedAccessSignature.Parse(string.Empty, EXPIRED_RAW_TOKEN));

        [Fact]
        [Unit]
        public void ParseThrowsWithWhiteSpaceIotHubNameTest() => Assert.Throws<ArgumentNullException>(() => SharedAccessSignature.Parse(WHITE_SPACE, EXPIRED_RAW_TOKEN));

        [Fact]
        [Unit]
        public void ParseThrowsWithNullRawTokenTest() => Assert.Throws<ArgumentNullException>(() => SharedAccessSignature.Parse(IOT_HUB_NAME, null));

        [Fact]
        [Unit]
        public void ParseThrowsWithEmptyRawTokenTest() => Assert.Throws<ArgumentNullException>(() => SharedAccessSignature.Parse(IOT_HUB_NAME, string.Empty));

        [Fact]
        [Unit]
        public void ParseThrowsWithWhiteSpaceRawTokenTest() => Assert.Throws<ArgumentNullException>(() => SharedAccessSignature.Parse(IOT_HUB_NAME, WHITE_SPACE));

        [Fact]
        [Unit]
        public void ParseThrowsWithBadRawTokenTest() => Assert.Throws<FormatException>(() => SharedAccessSignature.Parse(IOT_HUB_NAME, BAD_TOKEN));

        [Fact]
        [Unit]
        public void ParseThrowsWithSignatureMissingFromRawTokenTest() => Assert.Throws<FormatException>(() => SharedAccessSignature.Parse(IOT_HUB_NAME, BAD_TOKEN_MISSING_SIGNATURE));

        [Fact]
        [Unit]
        public void ParseThrowsWithExpiryMissingFromRawTokenTest() => Assert.Throws<FormatException>(() => SharedAccessSignature.Parse(IOT_HUB_NAME, BAD_TOKEN_MISSING_EXPIRY));

        [Fact]
        [Unit]
        public void ParseThrowsWithsrMissingFromRawTokenTest() => Assert.Throws<FormatException>(() => SharedAccessSignature.Parse(IOT_HUB_NAME, BAD_TOKEN_MISSING_ENCODING_AUDIENCE));

        [Fact]
        [Unit]
        public void ParseThrowsWithSharedAccessSignatureMissingFromRawTokenTest() => Assert.Throws<FormatException>(() => SharedAccessSignature.Parse(IOT_HUB_NAME, MISSING_SHAREDACCESSSIGNATURE_RAW_TOKEN));

        [Fact]
        [Unit]
        public void ParseThrowsWithExpiredRawTokenTest() => Assert.Throws<UnauthorizedAccessException>(() => SharedAccessSignature.Parse(IOT_HUB_NAME, EXPIRED_RAW_TOKEN));

        [Fact]
        [Unit]
        public void ParseDoesNotThrowWithNonExpiredRawTokenTest() => this.AssertNoThrow(() => SharedAccessSignature.Parse(IOT_HUB_NAME, this.GetNonExpiredToken()));

        [Fact]
        [Unit]
        public void AuthenticateThrowsWhenRuleIsNullTest()
        {
            var sas = SharedAccessSignature.Parse(IOT_HUB_NAME, this.GetNonExpiredToken());

            Assert.Throws<ArgumentNullException>(() => sas.Authenticate(null));
        }

        [Fact]
        [Unit]
        public void AuthenticateThrowsIsExpiredTest()
        {
            var sas = SharedAccessSignature.Parse(IOT_HUB_NAME, this.GetAlmostExpiredToken());

            var rule = new SharedAccessSignatureAuthorizationRule();

            Thread.Sleep(5 * 1000);

            Assert.Throws<UnauthorizedAccessException>(() => sas.Authenticate(rule));
        }

        [Fact]
        [Unit]
        public void AuthenticateThrowsInvalidSASTest()
        {
            var sas = SharedAccessSignature.Parse(IOT_HUB_NAME, this.GetNonExpiredToken());

            var rule = new SharedAccessSignatureAuthorizationRule();

            Assert.Throws<UnauthorizedAccessException>(() => sas.Authenticate(rule));
        }

        [Fact]
        [Unit]
        public void AuthenticateSucceedsWhenValidSASWithPrimaryKeyTest()
        {
            var sas = SharedAccessSignature.Parse(IOT_HUB_NAME, this.GetNonExpiredToken());

            var json = new JObject
            {
                ["keyName"] = "test",
                ["primaryKey"] = "test",
                ["secondaryKey"] = "YmFk"
            };

            var rule = JsonConvert.DeserializeObject<SharedAccessSignatureAuthorizationRule>(json.ToString());

            this.AssertNoThrow(() => sas.Authenticate(rule));
        }

        [Fact]
        [Unit]
        public void AuthenticateSucceedsWhenValidSASWithSecondaryKeyTest()
        {
            var sas = SharedAccessSignature.Parse(IOT_HUB_NAME, this.GetNonExpiredToken());

            var json = new JObject
            {
                ["keyName"] = "test",
                ["primaryKey"] = "YmFk",
                ["secondaryKey"] = "test"
            };

            var rule = JsonConvert.DeserializeObject<SharedAccessSignatureAuthorizationRule>(json.ToString());

            this.AssertNoThrow(() => sas.Authenticate(rule));
        }

        private string GetNonExpiredToken()
        {
            // Generate a valid looking sas token
            var expiry = this.GetExpiry(10).ToString();
            var sr = "test";

            var sig = SharedAccessSignature.ComputeSignature(Convert.FromBase64String("test"), sr, expiry);
            sig = WebUtility.UrlEncode(sig);

            return "SharedAccessSignature\nsig=" + sig + "&se=" + expiry + "&sr=" + sr;
        }

        private string GetAlmostExpiredToken() => "SharedAccessSignature\nsig=test&se=" + this.GetExpiry(2) + "&sr=test";

        private void AssertNoThrow(Func<object> test) => Assert.Null(Record.Exception(test));

        private void AssertNoThrow(Action test) => Assert.Null(Record.Exception(test));

        private int GetExpiry(int secondsInFuture) => (int)Math.Floor((DateTime.UtcNow.AddSeconds(secondsInFuture).Subtract(SharedAccessSignatureConstants.MaxClockSkew) - SharedAccessSignatureConstants.EpochTime).TotalSeconds);
    }
}
