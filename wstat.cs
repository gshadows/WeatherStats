using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Xml;

public class WeatherStats {
	private static TextWriter logFile;
	private static EncoderParameters encoderParameters;
	private static ImageCodecInfo codec;
	private static Picture background;
	private static Picture mask;

	private static int analyzedCount = 0;
	private static int width = 0, height = 0;
	private static int unparsedNow;

	private static int[] green = null;
	private static int[] yellow = null;
	private static int[] red = null;
	private static int[] wind = null;
	private static int[] unparsed = null;
	private static int[] overall = null;

	
	
	private static void createArrays() {
		int size = width * height;
		logFile.WriteLine("*** IMAGE SIZE: {0}x{1}", width, height);
		green = new int[size];
		yellow = new int[size];
		red = new int[size];
		wind = new int[size];
		unparsed = new int[size];
		overall = new int[size];
	}
	
	
	private struct Picture {
		public readonly byte[] bgr;
		public readonly int width, height, stride, bytesPerPixel;
		private Bitmap bmp;
		private PixelFormat pixelFormat;
		
		public Picture(string imagePath) {
			this.bmp = new Bitmap(imagePath);
			this.width = bmp.Width;
			this.height = bmp.Height;
			this.pixelFormat = bmp.PixelFormat;

			switch (pixelFormat) {
				case PixelFormat.Canonical:
				case PixelFormat.Format32bppArgb:
				case PixelFormat.Format32bppRgb:
					bytesPerPixel = 4;
					break;
				case PixelFormat.Format24bppRgb:
					bytesPerPixel = 3;
					break;
				default:
					throw new Exception("Unsupported pixel format: " + pixelFormat + " - " + imagePath);
			}
			
			BitmapData bmpData = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, pixelFormat);
			stride = bmpData.Stride;
			if (stride < 0) {
				throw new Exception("Unsupported bottom-top images (stride < 0)" + " - " + imagePath);
			}
			int byteSize = Math.Abs(stride) * height;
			bgr = new byte[byteSize]; // Declare an array to hold the bytes of the bitmap.

			logFile.WriteLine("Bitmap \"{0}\" format: {1} {2}x{3} stride {4}", imagePath, pixelFormat, width, height, stride);

			// Copy the BGR values into the array.
			System.Runtime.InteropServices.Marshal.Copy(bmpData.Scan0, bgr, 0, bgr.Length);
			bmp.UnlockBits(bmpData);
		}
		
		public Picture(int w, int h, int bytesPerPix) {
			this.width = w;
			this.height = h;
			this.bytesPerPixel = bytesPerPix;

			switch(bytesPerPixel) {
				case 3:
					pixelFormat = PixelFormat.Format24bppRgb;
					break;
				case 4:
					pixelFormat = PixelFormat.Format32bppArgb;
					break;
				default:
					throw new Exception("Unsupported bytes per pixel: " + bytesPerPixel);
			}

			bmp = new Bitmap(width, height, pixelFormat);
			
			BitmapData bmpData = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, pixelFormat);
			stride = bmpData.Stride;
			if (stride < 0) {
				throw new Exception("Unsupported bottom-top images (stride < 0)");
			}
			int byteSize = Math.Abs(stride) * height;
			bgr = new byte[byteSize]; // Declare an array to hold the bytes of the bitmap.

			bmp.UnlockBits(bmpData);
		}
		
		public void save(string imagePath) {
			BitmapData bmpData = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, pixelFormat);

			// Copy the BGR values back to the bitmap.
			System.Runtime.InteropServices.Marshal.Copy(bgr, 0, bmpData.Scan0, bgr.Length);
			
			bmp.UnlockBits(bmpData);
			
			bmp.Save(imagePath, codec, encoderParameters);
		}
		
		public int getBGR(int x, int y) {
			int ofs = (y * stride) + (x * bytesPerPixel);
			return (bgr[ofs] << 16) | (bgr[ofs + 1] << 8) | bgr[ofs + 2];
		}
	}
	
	
	private static void analyzePixel(int bgr, int ofs) {
		int b = (bgr >> 16) & 0xFF;
		int g = (bgr >> 8) & 0xFF;
		int r = bgr & 0xFF;
		
		if ((Math.Abs(r - g) <= 32) && (Math.Abs(r - b) <= 32) && (Math.Abs(g - b) <= 32)) {
			// Grayscale is background (land) - skip it.
			return;
		}
		if ((g > 248) && (b > 248) && (r > 200)) {
			// Light blue is background (sea) - skip it.
			return;
		}
		if (
			((b - g > 20) && (b - r > 32)) || // Dark blue - wind zone.
			((r < 16) && (g < 64) && (b > 100)) // Dark blue BORDER - wind zone.
		) {
			wind[ofs]++;
			return;
		}
		if (r - b > 64) {
			if ((r - g > 64)) {
				// Red area.
				red[ofs]++;
				return;
			} else {
				// Yellow area.
				yellow[ofs]++;
				return;
			}
		}
		if (((g - b) > 8) && ((g - r) > 24)) {
			// Green area.
			green[ofs]++;
			return;
		}
		// Otherwise it is some error or mixed colors on borders.
		unparsed[ofs]++;
		unparsedNow++;
		logFile.WriteLine("Unparsed: {0}, {1}, {2} at {3} ({7}, {8}) - r-b {4}, r-g {5}, g-b {6}", r, g, b, ofs, r-b, r-g, g-b, ofs % width, ofs / width);
	}
	
	
	private static void analyzeImageData(Picture pic) {
		int w = pic.width;
		if (w > width) w = width;
		
		int h = pic.height;
		if (h > height) h = height;
		
		for (int y = 0; y < h; y++) {
			for (int x = 0; x < w; x++) {
				int dataOffset = y * width + x;
				analyzePixel(pic.getBGR(x, y), dataOffset);
			}
		}
	}
	
	
	// Implements layers blend mode: addition.
	private static void applyMask(Picture pic) {
		byte[] p = pic.bgr;
		byte[] m = mask.bgr;
		int len = Math.Min(p.Length, m.Length);
		for (int i = 0; i < len; i++) {
			int sum = p[i] + m[i];
			p[i] = (byte)((sum > 255) ? 255 : sum);
		}
	}
	
	
	// Implements layers blend mode: multiplication.
	private static void applyBackground(Picture pic) {
		byte[] p = pic.bgr;
		byte[] b = background.bgr;
		int len = Math.Min(p.Length, b.Length);
		for (int i = 0; i < len; i++) {
			int mul = ((int)p[i] * (int)b[i]) >> 8;
			p[i] = (byte)((mul > 255) ? 255 : mul);
		}
	}
	
	
	private static void analyzeImage(string imagePath) {
		Picture pic = new Picture(imagePath);
		applyMask(pic);
		pic.save(imagePath.Replace("MeteoDiary", "results"));
		
		if (width == 0) {
			width = pic.width;
			height = pic.height;
			createArrays();
		}
		
		unparsedNow = 0;
		analyzeImageData(pic);
		logFile.WriteLine("Unparsed: {0}%", (int)(unparsedNow * 10000f / width / height) / 100f);
	}
	
	
	private static void analyzeImagesByMask(string imagesDir, string mask) {
		var images = Directory.EnumerateFiles(imagesDir, mask);
		foreach (string imagePath in images) {
			analyzeImage(imagePath);
			analyzedCount++;
		}
	}
	
	
	private static void analyzeImages(string imagesDir) {
		logFile.WriteLine("*** ANALYZING IMAGES...");
		analyzeImagesByMask(imagesDir, "*.jpg");
		analyzeImagesByMask(imagesDir, "*.jpeg");
		analyzeImagesByMask(imagesDir, "*.png");
		logFile.WriteLine("*** TOTAL FILES ANALYZED: {0}", analyzedCount);
	}
	
	
	private static void analyzeFinal() {
		for (int i = 0; i < overall.Length; i++) {
			overall[i] = red[i] * 4 + yellow[i] * 2 + green[i];
		}
	}
	
	
	delegate void Convert(int x, int y, out int bgr);
	
	
	private static void prepareResult(Picture pic, Convert convert) {
		int ofs = 0;
		int ofsY = 0;
		int bgr;
		bool hasAlpha = (pic.bytesPerPixel == 4);
		
		for (int y = 0; y < height; y++) {
			for (int x = 0; x < width; x++) {
				convert(x, y, out bgr);
				pic.bgr[ofs++] = (byte)((bgr >> 16) & 0xFF);
				pic.bgr[ofs++] = (byte)((bgr >> 8) & 0xFF);
				pic.bgr[ofs++] = (byte)(bgr & 0xFF);
				if (hasAlpha) pic.bgr[ofs++] = 0; // Unused alpha.
			}
			ofsY += pic.stride;
			ofs = ofsY;
		}
	}
	
	
	private static void mapColor(float v, int bgrMin, int bgrMax, out int bgr) {
		int bmin = ((bgrMin >> 16) & 0xFF);
		int gmin = ((bgrMin >> 8) & 0xFF);
		int rmin = (bgrMin & 0xFF);
		int b = ((bgrMax >> 16) & 0xFF) - bmin;
		int g = ((bgrMax >> 8) & 0xFF) - gmin;
		int r = (bgrMax & 0xFF) - rmin;
		b = ((int)(b * v) + bmin) & 0xFF;
		g = ((int)(g * v) + gmin) & 0xFF;
		r = ((int)(r * v) + rmin) & 0xFF;
		bgr = (b << 16) | (g << 8) | r;
	}
	
	
	private struct ColorMap {
		public readonly float val;
		public readonly int bgr;
		public ColorMap(float val, int bgr) { this.val = val; this.bgr = bgr; }
	}
	private static ColorMap[] mainColors = {
		new ColorMap( 0.00f, 0xFFFFFF ), // White.
		new ColorMap( 0.25f, 0x00FF00 ), // Green.
		new ColorMap( 0.50f, 0x00FFFF ), // Yellow.
		new ColorMap( 0.75f, 0x0000FF ), // Red.
		new ColorMap( 1.00f, 0xFF00FF ), // Magenta.
	};
	
	
	private static void mapFrequencyColors(float average, int x, int y, ColorMap[] colors, out int bgr) {
		if (average <= 0f) {
			bgr = 0xFFFFFF; //background.getBGR(x, y); // Background.
		} else {
			float lowVal = colors[0].val;
			int minColor = colors[0].bgr;
			foreach (ColorMap map in colors) {
				if (average < map.val) {
					float correctedAverage = (average - lowVal) * (map.val - lowVal);
					mapColor(average, minColor, map.bgr, out bgr);
					//logFile.WriteLine("avg {0} < {1} -> {2} -> {3:X6}", average, map.val, correctedAverage, bgr);
					return;
				} else {
					lowVal = map.val;
					minColor = map.bgr;
				}
			}
			bgr = colors[colors.Length - 1].bgr;
			//logFile.WriteLine("avg {0} other -> {1:X6}", average, bgr);
		}
	}
	
	
	private static void outputResult(string outName, bool needBg, Convert convert) {
		Picture pic = new Picture(width, height, 3);
		prepareResult(pic, convert);
		if (needBg) applyBackground(pic);
		pic.save(outName);
	}
	
	
	private static void outputResults(string outDir, string ext) {
		if ((width <= 0) || (height <= 0)) {
			return; // No images was parsed. Size unknown.
		}
		if (!outDir.EndsWith("\\")) {
			outDir += "\\";
		}

		logFile.WriteLine("Generating image: overall");
		outputResult(outDir + "overall" + ext, true, (int x, int y, out int bgr) => {
			float average = overall[y * width + x] / 4f / analyzedCount;
			mapFrequencyColors(average, x, y, mainColors, out bgr);
		});

		logFile.WriteLine("Generating image: wind");
		outputResult(outDir + "wind" + ext, true, (int x, int y, out int bgr) => {
			float average = wind[y * width + x] / 4f / analyzedCount;
			mapFrequencyColors(average, x, y, mainColors, out bgr);
		});

		logFile.WriteLine("Generating image: unparsed");
		int unparsedCount = 0;
		outputResult(outDir + "unparsed" + ext, false, (int x, int y, out int bgr) => {
			int v = unparsed[y * width + x];
			bgr = (v > 0) ? 0xFFFFFF : 0x000000; // White at unparsed, black others.
			if (v > 0) unparsedCount++;
		});
		logFile.WriteLine("Total Unparsed: {0}%", (int)(unparsedCount * 10000f / width / height / analyzedCount) / 100f);
	}
	
	
	private static void prepareEncoder(string mime) {
		// Prepare output image codec.
		ImageCodecInfo[] encoders = ImageCodecInfo.GetImageEncoders();
		foreach (ImageCodecInfo imageCodec in encoders) {
			if (imageCodec.MimeType == mime) {
				codec = imageCodec;
				encoderParameters = new EncoderParameters(1);
				encoderParameters.Param[0] = new EncoderParameter(Encoder.Quality, 99L); // 99% quality JPEG.
				break;
			}
        }
	}
	
	
	public static void Main(string[] args)
	{
		if (args.Length < 4) {
			Console.WriteLine("Usage: wstat <empty_img> <mask_img> <images_dir> <out_dir> [output.log]");
			return;
		}
		logFile = (args.Length > 4) ? new StreamWriter(args[4], false, System.Text.Encoding.UTF8) : Console.Out;
		
		prepareEncoder("image/jpeg");

		background = new Picture(args[0]);
		mask = new Picture(args[1]);
		
		analyzeImages(args[2]);
		analyzeFinal();
		outputResults(args[3], ".jpg");

		logFile.Flush();
		logFile.Close();
	}
}
