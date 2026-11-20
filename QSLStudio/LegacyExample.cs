using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace QslStudio
{
    // Match old default field values only in projects marked as examples.
    // Fingerprints let us preserve user edits without shipping the old contact details.
    internal static class LegacyExample
    {
        private static readonly HashSet<string> Defaults = new HashSet<string>(StringComparer.Ordinal)
        {
            // own
            "yzDE/sqGz6TAmP3Jxf07uypDI1b3Wwk62Xz+dNjfO9g=",
            // location
            "uH1zDKg6i0RE7iaToZPXuK69gwf8Y+6f24Gtrw92xds=",
            // operator
            "vl0Wvm6CMDg6BPretVgFWiLV94/VRYk82C2a1ZVvSng=",
            // address
            "VSp2oHMolS/0zn/abgUol/PHMSTNPXlNhw0ydx3+YIo=",
            "uR8jz8p1ue3KWHQwC9lzdpnwS7HpKp/GFoO5oAQ9FR4=",
            // email
            "6r9JzUEyxxZ2JiI43U2qfeJbPfvVtNHHwKcI3fyZ7jY=",
            // signature
            "zx2KDIZnjkjhVz2cYnEzGZFeCpTVdPlK5DlGaz3/f7E=",
            // grid
            "7NLaRNVdhlOkXGLkKO6jMAL2UZzZkDQjRpp4CMfoRoE=",
            // cq
            "4L07rDKbK4b7xZ7bdF2zAZV3UAUNQOvPOJeL4YFmLDk=",
            // itu
            "kEeTSQKBzvsaOQN2FSLNepM0OXUG7QwM35VL2optox4=",
            // activity
            "xwOBUdsnKwGjt4yOXCe5Ngrs/VEY7F9Ei2OZjvv+Vas=",
            "8vGZLixWAPG++Czn/qdxnI4koCYdYuW1oCMcbcPLlTk=",
            "6hsZy4dCGOH3WeexyTodhXuXthk9WIq1p1r+ioqOdHs=",
            "vM2KMD/lWcg1YrG9QvDuq6IEW47lcGe6PjfQFY2/BoA=",
            // reference
            "oJPhBiZlYg1pzPvDHHvD3vH9I8YWSv4Ws+DSxRez2Y8=",
            "zqg2UilPJdOqstrq6jIaGGrWA5UMngiW+Gd1ri1y4t4=",
            "ezgCXgI+iRCOsPdHrFkAKpd7xko4Oatq2w0og6vX6EE=",
            // other
            "MzeJbn+OCKCbCKv20ygeyM99Qt1fIQ9tEHdPKyIZYJY=",
            "2hQxsdLGajk4qpD9uWoXYB7vScK7f0/yY97zOBGVsCo=",
            // date
            "zYUkfY78phO0egm5dp2YZfh3F2UNQJB+s3FsgJ6/Lg8=",
            // utc
            "27HrdQVtcJCtIIdHwCPrJNx3qmVBcmv7lULwAq0j6Lc=",
            // mhz
            "+l5uV0pnKnQFiBOfSNe4nGUITQnSa1u3ZPLqAOHgFAk=",
            "Yi7//OQVB6J28vnsFkeU2lQF2Sc+TcdcfqIRmrDur0s=",
            // mode
            "WJ64HQquWBYfaGXfCL4StT1O1EagcAMaR7hcT3G6vdc=",
            "qcLDao5HDUDKAqNr2+jMnX9o9bcjA0GgjKsk+md+1cw=",
            // rst
            "ZnI6cknzAKUMAuGFiVA/VVyTNniN3a0Pt/U7T+wVav4=",
            // confirm
            "Cfah7gFQm/I80g0G6TalHsqrN1bWrpouSdUiv9T2HmE=",
            // via
            "pW14E4Trl3z1YwAA5/Vtw0CL3u33C7mZ79tk6CQM6PE=",
            // reply
            "DKUSj2FB/n+5qenbtgE4wmrfOOUTXz1lIyjKFUooaqA=",
            // note
            "5DJ8yu9UCYJ9pstbZFIFVnOTiLThhwp3Gsu42TM1sBM=",
            "rY5hnkAZnWlbIkJYrPEVXd/VTnmec1bXB0SFn7MomLg=",
            "5J1z7/HePxuEXOWd7fJ6kmnNBmHAbfe16tBn4PNIglA=",
            // date2
            "Y6Eet/BkBq+qa0dNh61nGr7aN+q7JRvGCKsYHs7yyTI=",
            // utc2
            "DJdo0vNUfYkRMrGLw8Zs6K9iAhdRTDu/tpba4f364Ic=",
            // mhz2
            "SFyK6LiBxvsZgw/dOGCnYfpXTPIYQzfwDkizV8T6QlI=",
            // mode2
            "YrlPVCG0mJqf5V4bmHX6+1ZMSkO2btY83F4vqxJzAz4=",
            // rst2
            "CjqxsgthOPiExUhpmGwzvN17PdTHnJHXNJvbASZ8DEA=",
            // worked
            "6YyHMpHX/zG+VTggvkGe8JJ3UB95x9PE1j9XLJBFh2U=",
            // sat
            "y2KoEsOw8A0YQRih2cfCJjmHTu8GRD7UvoEfIfP5/f0=",
            // mhzRx
            "O96ZzwcYA7qLtbuPpCCMr1iQosXv9DoCrj8JUR6ZPnw=",
            // otherGrid
            "ohdEaAtmao32tGj9XB4G9o/vauaDnEg+Qz7i7DMQC9k=",
            // recipient
            "cqA49YbXfAxCIT+8iwKIvsJYWnDwUPaOFxJm9TY09PQ=",
            "NPpbFAsuOB/ihUfF3psha/NIlg5Oiszh9Uyyj1n/8kc=",
            // venue
            "DrEczJ8LkMD/Ay57T+9O6qTCWrQeMfFollN/N+k1OW8=",
            // stx
            "pfV12fgcJQCv30m+9In46OZBy2/IwdRfzH334BBw9hc=",
            // srx
            "jfu+QaZ5Dz7LfRU+IQuLDwLlU6SfiB9DUuCfvwFXkaI=",
            // postcode
            "alPz0zbR9j59gef9Pmp5/kVwpOmrLQtuLjNVkNu8pFY=",
        };

        internal static bool IsDefault(string key, string value)
        {
            if (value.Length == 0) return false;
            byte[] bytes = Encoding.UTF8.GetBytes(key + "\0" + value.Replace("\r\n", "\n"));
            using (var sha = SHA256.Create())
                return Defaults.Contains(Convert.ToBase64String(sha.ComputeHash(bytes)));
        }

        internal static bool IsPlaceholderCall(string call)
        {
            switch (call.Trim().ToUpperInvariant())
            {
                case "DEMO":
                case "SWL DEMO":
                case "BA1XXX":
                case "BY6XXX":
                case "BG4XXX":
                case "SWL-XXX":
                case "BA1XYZ":
                case "BY6XYZ":
                case "BG4XYZ":
                case "DEMO-01":
                case "DEMO-02":
                    return true;
                default:
                    return false;
            }
        }
    }
}
