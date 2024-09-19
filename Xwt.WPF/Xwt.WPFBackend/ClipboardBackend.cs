// 
// ClipboardBackend.cs
//  
// Author:
//       Eric Maupin <ermau@xamarin.com>
// 
// Copyright (c) 2012 Xamarin Inc
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Collections.Specialized;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Xwt.Backends;
using WindowsClipboard = System.Windows.Clipboard;

namespace Xwt.WPFBackend
{
	public class WpfClipboardBackend
		: ClipboardBackend {

		// Clipboard may be locked by another process. In order to overcome this, it can be queried repeatedly.
		// See https://stackoverflow.com/questions/12769264/openclipboard-failed-when-copy-pasting-data-from-wpf-datagrid
		// And https://www.infragistics.com/community/forums/f/ultimate-ui-for-wpf/35379/failed-to-copy-clipbrd_e_cant_open
		// The wrapper below implements retry logic that should make it more reliable.
		const int MaxRetries = 10;
		const int SleepTime = 10;
		private static void CatchExceptionsAndRetry(Action action) {
			int retryCount = 0;
			Exception exception = null;
			while(retryCount < MaxRetries) {
				try {
					action.Invoke();
					return;
				} catch(Exception e) {
					exception = e;
					if(exception.Message.Contains("CLIPBRD_E_BAD_DATA")) {
						// do not retry
						break;
					}
					System.Threading.Thread.Sleep(SleepTime);
					retryCount++;
				}
			}

			throw exception;
		}

		public override void Clear() {
			CatchExceptionsAndRetry(() => {
				WindowsClipboard.Clear();
			});
		}

		
		private void SetClipboardMultipleFormats(string text, string textHtml, Xwt.Drawing.Image image) {

			// WIN-9879 indicates that the Keza clipboard code is more robust that the XWT clipboard code and so
			// this method was imported in from Keza.Windows.WindowsClipboardHelper on Oct 12, 2023.
			
			DataObject dataObject = new DataObject();
			if(text != null) {
				dataObject.SetText(text);
			}
			if(textHtml != null) {
				dataObject.SetData(DataFormats.Html, GenerateCFHtml(textHtml));
			}
			if(image != null) {
				var src = image.ToBitmap().GetBackend() as WpfImage;
				var imageSource = src.MainFrame;
				BitmapSource bitmapSource = ConvertToBitmapSource(imageSource);
				dataObject.SetImage(bitmapSource);
			}

			CatchExceptionsAndRetry(() => {
				WindowsClipboard.SetDataObject(dataObject);
			});
		}
		
		private BitmapSource ConvertToBitmapSource(ImageSource imageSource) {
			if(imageSource is BitmapSource) {
				return imageSource as BitmapSource;
			} else {
				// Create a RenderTargetBitmap and draw the ImageSource onto it.
				RenderTargetBitmap rtb = new RenderTargetBitmap((int)imageSource.Width, (int)imageSource.Height, 96, 96, PixelFormats.Pbgra32);
				DrawingVisual drawingVisual = new DrawingVisual();

				using(System.Windows.Media.DrawingContext drawingContext = drawingVisual.RenderOpen()) {
					drawingContext.DrawImage(imageSource, new Rect(0, 0, imageSource.Width, imageSource.Height));
				}

				rtb.Render(drawingVisual);
				return rtb;
			}
		}

		
		public override void SetData(TransferDataType type, Func<object> dataSource, bool cleanClipboardFirst = true) {
			if(type == null)
				throw new ArgumentNullException("type");
			if(dataSource == null)
				throw new ArgumentNullException("dataSource");

			if(cleanClipboardFirst) {
				Clear();
			}

			if (type == TransferDataType.Html) {
				SetClipboardMultipleFormats(null, dataSource().ToString(), null);
			} else if (type == TransferDataType.Image) {
				var img = dataSource() as Xwt.Drawing.Image;
				if (img != null) {
					SetClipboardMultipleFormats(null, null, img);
				}
			} else if(type == TransferDataType.Uri) {
				CatchExceptionsAndRetry(() => {
					WindowsClipboard.SetFileDropList((StringCollection)(dataSource()));
				});
			} else if(type == TransferDataType.Text) {
				SetClipboardMultipleFormats(dataSource().ToString(), null, null);
			} else {
				CatchExceptionsAndRetry(() => {
					WindowsClipboard.SetData(type.ToWpfDataFormat(), dataSource());
				});
			}
		}

		static readonly string emptyCFHtmlHeader = GenerateCFHtmlHeader (0, 0, 0, 0);

		/// <summary>
		/// Generates a CF_HTML cliboard format document
		/// </summary>
		string GenerateCFHtml (string htmlFragment)
		{
			int startHTML     = emptyCFHtmlHeader.Length;
			int startFragment = startHTML;
			int endFragment   = startFragment + System.Text.Encoding.UTF8.GetByteCount (htmlFragment);
			int endHTML       = endFragment;
			return GenerateCFHtmlHeader (startHTML, endHTML, startFragment, endFragment) + htmlFragment;
		}

		/// <summary>
		/// Generates a CF_HTML clipboard format header.
		/// </summary>
		static string GenerateCFHtmlHeader (int startHTML, int endHTML, int startFragment, int endFragment)
		{
			return
				"Version:0.9" + Environment.NewLine +
					string.Format ("StartHTML: {0:d8}", startHTML) + Environment.NewLine +
					string.Format ("EndHTML: {0:d8}", endHTML) + Environment.NewLine +
					string.Format ("StartFragment: {0:d8}", startFragment) + Environment.NewLine +
					string.Format ("EndFragment: {0:d8}", endFragment) + Environment.NewLine;
		}

		public override bool IsTypeAvailable (TransferDataType type)
		{
			if (type == null)
				throw new ArgumentNullException ("type");

			if (type == TransferDataType.Text) {
				bool containsFileDropList = false;
				CatchExceptionsAndRetry(() => {
					containsFileDropList = WindowsClipboard.ContainsFileDropList();
				});
				if(containsFileDropList) {
					StringCollection stringCollection = null;
					CatchExceptionsAndRetry(() => {
						stringCollection = WindowsClipboard.GetFileDropList();
					});
					foreach(string s in stringCollection) {
						return true;
					}
				}
			}

			bool val = false;
			CatchExceptionsAndRetry(() => {
				val = WindowsClipboard.ContainsData(type.ToWpfDataFormat());
			});
			return val;
		}


		public static BitmapSource TryFixAlphaChannel (BitmapSource bitmapImage) {
			double dpi = 96;
			int width = bitmapImage.PixelWidth;
			int height = bitmapImage.PixelHeight;

			int stride = width * (bitmapImage.Format.BitsPerPixel + 7) / 8;
			byte[] pixelData = new byte[stride * height];
			bitmapImage.CopyPixels (pixelData, stride, 0);

			if (bitmapImage.Format == System.Windows.Media.PixelFormats.Bgra32) {
				bool anyNonZeroAlpha = false;
				for (int y = 0; y < height; y++) {
					for (int o = 3; o < stride; o += 4) {
						// Bgra32, so set the Alpha
						if (pixelData[y*stride + o] > 0) {
							anyNonZeroAlpha = true;
							break;
						}
					}
					if (anyNonZeroAlpha) {
						break;
					}
				}
				if (!anyNonZeroAlpha) {
					for (int y = 0; y < height; y++) {
						for (int o = 3; o < stride; o += 4) {
							// Bgra32, so set the Alpha
							pixelData[y*stride + o] = 255;
						}
					}
				}
			}

			return BitmapSource.Create (width, height, dpi, dpi, bitmapImage.Format, bitmapImage.Palette, pixelData, stride);
		}

		public static BitmapSource GetBestPossibletAlphaBitmapFromDataObject(System.Windows.IDataObject ob) {
			var formats = ob.GetFormats();
			BitmapSource bmp = null;

			foreach (string f in formats) {
				if (f != "PNG" && f != "image/png") { // only PNG (GIMP) and image/png (haven't seen this in the wild but could be useful)
					continue;
				}
				var it = ob.GetData(f);
				var ms = it as MemoryStream;
				if (ms != null) {
					BitmapImage result = new BitmapImage();
					result.BeginInit();
					// According to MSDN, "The default OnDemand cache option retains access to the stream until the image is needed."
					// Force the bitmap to load right now so we can dispose the stream.
					result.CacheOption = BitmapCacheOption.OnLoad;
					result.StreamSource = ms;
					result.EndInit();
					result.Freeze();
					bmp = result;
					break;
				}
			}

			if (bmp == null) {
				foreach (string f in formats) {
					if (!f.ToLower().Contains("bitmap")) {
						continue;
					}
					var obj = ob.GetData(f) as BitmapSource;
					if (obj != null) {
						bmp = obj;
						break;
					}
				}
			}

			if (bmp == null) {
				CatchExceptionsAndRetry(() => {
					bmp = WindowsClipboard.GetImage();
				});
			}
			bmp = TryFixAlphaChannel(bmp);
			return bmp;
		}

		public override object GetData (TransferDataType type)
		{
			if (type == null)
				throw new ArgumentNullException ("type");

			if (!IsTypeAvailable (type))
				return null;

			if (type == TransferDataType.Text) {
				bool containsFileDropList = false;
				CatchExceptionsAndRetry(() => {
					containsFileDropList = WindowsClipboard.ContainsFileDropList();
				});
				if(containsFileDropList) {
					StringCollection stringCollection = null;
					CatchExceptionsAndRetry(() => {
						stringCollection = WindowsClipboard.GetFileDropList();
					});
					foreach(string s in stringCollection) {
						return "file://" + s;
					}
				}
			}

			if(type == TransferDataType.Image) {
				IDataObject ob = null;
				CatchExceptionsAndRetry(() => {
					ob = WindowsClipboard.GetDataObject();
				});
				var bmp = GetBestPossibletAlphaBitmapFromDataObject(ob);
				return ApplicationContext.Toolkit.WrapImage(bmp);
			}

			object result = null;
			CatchExceptionsAndRetry(() => {
				result = WindowsClipboard.GetData(type.ToWpfDataFormat());
			});
			return result;
		}

		public override IAsyncResult BeginGetData (TransferDataType type, AsyncCallback callback, object state)
		{
			if (type == null)
				throw new ArgumentNullException ("type");
			if (callback == null)
				throw new ArgumentNullException ("callback");

			return Task<object>.Factory.StartNew (s => GetData (type), state)
				.ContinueWith (t => callback (t));
		}

		public override object EndGetData (IAsyncResult ares)
		{
			if (ares == null)
				throw new ArgumentNullException ("ares");

			Task<object> t = ares as Task<object>;
			if (t == null)
				throw new ArgumentException ("ares is the incorrect type", "ares");

			return t.Result;
		}
	}
}
