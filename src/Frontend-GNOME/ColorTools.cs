/*
 * $Id: ChannelPage.cs 138 2006-12-23 17:11:57Z meebey $
 * $URL: svn+ssh://svn.qnetp.net/svn/smuxi/smuxi/trunk/src/Frontend-GNOME/ChannelPage.cs $
 * $Rev: 138 $
 * $Author: meebey $
 * $Date: 2006-12-23 18:11:57 +0100 (Sat, 23 Dec 2006) $
 *
 * Smuxi - Smart MUltipleXed Irc
 *
 * Copyright (c) 2008 Mirco Bauer <meebey@meebey.net>
 *
 * Full GPL License: <http://www.gnu.org/licenses/gpl.txt>
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 2 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307 USA
 */

using System;
using System.Collections.Generic;
using System.Globalization;
using Smuxi.Common;
using Smuxi.Engine;

namespace Smuxi.Frontend.Gnome
{
    public static class ColorTools
    {
#if LOG4NET
        private static readonly log4net.ILog f_Logger = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
#endif
        private static Dictionary<int, TextColor> f_BestContrastColors;

        static ColorTools() {
            f_BestContrastColors = new Dictionary<int, TextColor>(1024);
        }

        public static string GetHexCodeColor(Gdk.Color color)
        {
            /*
            // this approach is changing the color instead of converting it, as byte wraps
            string hexcode = String.Format("{0}{1}{2}",
                                           ((byte) color.Red).ToString("X2"),
                                           ((byte) color.Green).ToString("X2"),
                                           ((byte) color.Blue).ToString("X2"));
            */
            string hexcode = String.Format("#{0}{1}{2}",
                                           (color.Red >> 8).ToString("X2"),
                                           (color.Green >> 8).ToString("X2"),
                                           (color.Blue >> 8).ToString("X2"));
            return hexcode;
        }
        
        public static TextColor GetTextColor(Gdk.Color color)
        {
            string hexcode = GetHexCodeColor(color);
            // remove leading "#" character
            hexcode = hexcode.Substring(1);
            int value  = Int32.Parse(hexcode, NumberStyles.HexNumber);
            return new TextColor(value);
        }
        
        public static Gdk.Color GetGdkColor(TextColor textColor)
        {
            if (textColor == null) {
                throw new ArgumentNullException("textColor");
            }

            return GetGdkColor(textColor.HexCode);
        }

        public static Gdk.Color GetGdkColor(string hexCode)
        {
            Trace.Call(hexCode);

            var color = TextColor.Parse(hexCode);
            return new Gdk.Color(color.Red, color.Green, color.Blue);
        }

        public static TextColor GetBestTextColor(TextColor fgColor, TextColor bgColor)
        {
            return GetBestTextColor(fgColor, bgColor, ColorContrast.Medium);
        }

        public static TextColor GetBestTextColor(TextColor fgColor,
                                                 TextColor bgColor,
                                                 ColorContrast neededContrast)
        {
            // logging noise
            //Trace.Call(fgColor, bgColor, neededContrast);

            if (fgColor == null) {
                throw new ArgumentNullException("fgColor");
            }
            if (bgColor == null) {
                throw new ArgumentNullException("bgColor");
            }

            TextColor bestColor;
            int key = fgColor.Value ^ bgColor.Value ^ (int) neededContrast;
            if (f_BestContrastColors.TryGetValue(key, out bestColor)) {
                return bestColor;
            }

            double brDiff = GetBritnessDifference(bgColor, TextColor.White);
            int modifier = 0;
            // for bright backgrounds we need to go from bright to dark colors
            // for better contrast and for dark backgrounds the opposite
            if (brDiff < 127) {
                // bright background
                modifier = -10;
            } else {
                // dark background
                modifier = 10;
            }

            double lastDifference = 0;
            bestColor = fgColor;
            int attempts = 1;
            while (true) {
                double difference = GetLuminanceDifference(bestColor, bgColor);
                double needed = ((int) neededContrast) / 10d;
                if (difference > needed) {
                    break;
                }

#if LOG4NET
                /* logging noise
                f_Logger.Debug("GetBestTextColor(): color has bad contrast: " +
                               bestColor + " difference: " + difference +
                               " needed: " + needed);
                */
#endif

                // change the fg color
                int red   = bestColor.Red   + modifier;
                int green = bestColor.Green + modifier;
                int blue  = bestColor.Blue  + modifier;

                // cap to allowed values
                if (modifier > 0) {
                    if (red > 255) {
                        red = 255;
                    }
                    if (green > 255) {
                        green = 255;
                    }
                    if (blue > 255) {
                        blue = 255;
                    }
                } else {
                    if (red < 0) {
                        red = 0;
                    }
                    if (green < 0) {
                        green = 0;
                    }
                    if (blue < 0) {
                        blue = 0;
                    }
                }

                bestColor = new TextColor((byte) red, (byte) green, (byte) blue);
                
                // in case we found no good color
                if (bestColor == TextColor.White ||
                    bestColor == TextColor.Black) {
                    break;
                }
                attempts++;
            }
#if LOG4NET
            f_Logger.Debug(
                String.Format(
                    "GetBestTextColor(): found good contrast: {0}|{1}={2} " +
                    "({3}) attempts: {4}", fgColor, bgColor,  bestColor,
                    neededContrast, attempts
                )
            );
#endif
            f_BestContrastColors.Add(key, bestColor);

            return bestColor;
        }

        // algorithm ported from PHP to C# from:
        // http://www.splitbrain.org/blog/2008-09/18-calculating_color_contrast_with_php
        public static double GetLuminanceDifference(TextColor color1, TextColor color2)
        {
            double L1 = 0.2126d * Math.Pow(color1.Red   / 255d, 2.2d) +
                        0.7152d * Math.Pow(color1.Green / 255d, 2.2d) +
                        0.0722d * Math.Pow(color1.Blue  / 255d, 2.2d);
            double L2 = 0.2126d * Math.Pow(color2.Red   / 255d, 2.2d) +
                        0.7152d * Math.Pow(color2.Green / 255d, 2.2d) +
                        0.0722d * Math.Pow(color2.Blue  / 255d, 2.2d);
            if (L1 > L2) {
                return (L1 + 0.05d) / (L2 + 0.05d);
            } else {
                return (L2 + 0.05d) / (L1 + 0.05d);
            }
        }

        public static double GetBritnessDifference(TextColor color1, TextColor color2)
        {
            double br1 = (299d * color1.Red +
                          587d * color1.Green +
                          114d * color1.Blue) / 1000d;
            double br2 = (299d * color2.Red +
                          587d * color2.Green +
                          114d * color2.Blue) / 1000d;
            return Math.Abs(br1 - br2);
        }
    }
}