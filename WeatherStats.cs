using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Xml;

public class WeatherStats {
	public static TextWriter logFile;
	
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
	
	
	private static void analyzePixel(int bgr, int ofs) {
		int b = (bgr >> 16) & 0xFF;
		int g = (bgr >> 8) & 0xFF;
		int r = bgr & 0xFF;
		
		if ((Math.Abs(r - g) <= 16) && (Math.Abs(r - b) <= 16) && (Math.Abs(g - b) <= 16)) {
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
		//logFile.WriteLine("Unparsed: {0}, {1}, {2} at {3} ({7}, {8}) - r-b {4}, r-g {5}, g-b {6}", r, g, b, ofs, r-b, r-g, g-b, ofs % width, ofs / width);
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
	
	
	private static void analyzeImage(string imagePath) {
		Picture pic = new Picture(imagePath);
		if (mask != null) {
			pic.blendAdd(mask);
		}
		pic.saturate(2.0);
		pic.save(codec, encoderParameters, imagePath.Replace("MeteoDiary", "results"));
		
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
			bgr = 0xFFFFFF; // Background.
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
		if (needBg && (background != null)) {
			pic.blendMultiply(background);
		}
		pic.save(codec, encoderParameters, outName);
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
		if (!Options.parse(args)) {
			return;
		}
		string logName = Options.get("log");
		if (logName != null) {
			logFile = new StreamWriter(logName, false, System.Text.Encoding.UTF8);
		} else {
			logFile = Console.Out;
		}
		
		string bgName = Options.get("bg");
		if (bgName != null) {
			background = new Picture(bgName);
		}
		string maskName = Options.get("mask");
		if (maskName != null) {
			mask = new Picture(maskName);
		}
		
		prepareEncoder("image/jpeg");

		analyzeImages(Options.get("imgdir"));
		analyzeFinal();
		outputResults(Options.get("outdir"), ".jpg");

		logFile.Flush();
		logFile.Close();
	}
}
