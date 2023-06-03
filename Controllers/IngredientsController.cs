﻿using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Rendering;
using CapstoneProject.Models;
using Microsoft.AspNetCore.Authorization;
using Firebase.Auth;
using Firebase.Storage;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Webp;
using Libwebp.Standard;
using Microsoft.AspNetCore.WebUtilities;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using MailKit;
using System.Text.RegularExpressions;

namespace CapstoneProject.Controllers {

    [Authorize(Roles = "Admin")]
    public class IngredientsController : Controller {

        private readonly RecipeOrganizerContext _context;

        public static string ApiKey = "AIzaSyDIXdDdvo8NguMgxLvn4DWMNS-vXkUxoag";
        public static string Bucket = "cookez-cloud.appspot.com";
        public static string AuthEmail = "cookez.mail@gmail.com";
        public static string AuthPassword = "cookez";

        public IngredientsController(RecipeOrganizerContext context) {
            _context = context;
        }

        public IActionResult Index() {
            IEnumerable<Ingredient> objIngredient = _context.Ingredients.ToList();
            return View(objIngredient);
        }

        // GET
        public IActionResult Create() {
            ViewData["FkCategoryId"] = new SelectList(_context.IngredientCategories, "Id", "Name");
            return View();
        }

        //POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Ingredient model) {
            if (ModelState.IsValid) {
                if (model.file != null && model.file.Length > 0) {
                    IFormFile file = model.file;
                    // Generate a unique file name
                    string uniqueFileName = Guid.NewGuid().ToString() + "_" + file.FileName;

                    // Upload the file to Firebase Storage
                    string imageUrl = await UploadFirebase(file.OpenReadStream(), uniqueFileName);
                    model.ImgPath = imageUrl;
                    _context.Ingredients.Add(model);
                    await _context.SaveChangesAsync();
                    TempData["success"] = "Ingredient created successfully";
                    return RedirectToAction("Index");
                }
            }
            return View(model);
        }

        public static async Task<string> UploadFirebase(Stream stream, string fileName) {
            string imageFromFirebaseStorage = "";

            using (Image image = Image.Load(stream)) {

                // Resize the image to a smaller size if needed
                int maxWidth = 1000; // Set your desired maximum width here
                int maxHeight = 1000; // Set your desired maximum height here
                if (image.Width > maxWidth || image.Height > maxHeight) {
                    image.Mutate(x => x.Resize(new ResizeOptions {
                        Size = new Size(maxWidth, maxHeight),
                        Mode = ResizeMode.Max
                    }));
                }

                // Compress the image
                IImageEncoder imageEncoder;
                string fileExtension = Path.GetExtension(fileName).ToLower();
                if (fileExtension == ".png") {
                    imageEncoder = new PngEncoder { CompressionLevel = PngCompressionLevel.BestCompression };
                } else if (fileExtension == ".webp") {
                    imageEncoder = new SixLabors.ImageSharp.Formats.Webp.WebpEncoder { Quality = 100 }; // Adjust the quality level as needed
                } else {
                    imageEncoder = new JpegEncoder { Quality = 80 }; // Adjust the quality level as needed
                }

                using (MemoryStream webpStream = new MemoryStream()) {
                    image.Save(webpStream, new SixLabors.ImageSharp.Formats.Webp.WebpEncoder());

                    webpStream.Position = 0;

                    FirebaseAuthProvider firebaseConfiguration = new(new FirebaseConfig(ApiKey));

                    FirebaseAuthLink authConfiguration = await firebaseConfiguration
                        .SignInWithEmailAndPasswordAsync(AuthEmail, AuthPassword);

                    CancellationTokenSource cancellationToken = new();

                    FirebaseStorageTask storageManager = new FirebaseStorage(
                        Bucket,
                        new FirebaseStorageOptions {
                            AuthTokenAsyncFactory = () => Task.FromResult(authConfiguration.FirebaseToken),
                            ThrowOnCancel = true
                        })
                        .Child("images")
                        .Child("ingredients")
                        .Child(fileName)
                        .PutAsync(webpStream, cancellationToken.Token);

                    try {
                        imageFromFirebaseStorage = await storageManager;
                        firebaseConfiguration.Dispose();
                        return imageFromFirebaseStorage;
                    } catch (Exception ex) {
                        Console.WriteLine("Exception was thrown: {0}", ex);
                        return null;
                    }
                }
            }
        }

        //GET
        public IActionResult Delete(int? id) {
            if (id == null || id == 0) {
                return NotFound();
            }
            var ingredientFromDb = _context.Ingredients.Find(id);
            if (ingredientFromDb == null) {
                return NotFound();
            }
            return View(ingredientFromDb);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeletePOST(int? id) {
            var obj = _context.Ingredients.Find(id);
            if (obj == null) {
                return NotFound();
            }

            await DeleteFromFirebaseStorage(obj.ImgPath);

            _context.Ingredients.Remove(obj);
            _context.SaveChanges();
            TempData["success"] = "Ingredient deleted successfully";
            return RedirectToAction("Index");
        }


        private async Task DeleteFromFirebaseStorage(string fileName) {
            if (!string.IsNullOrEmpty(fileName)) {
                string exactFileName = GetImageNameFromUrl(fileName);
                FirebaseAuthProvider firebaseConfiguration = new FirebaseAuthProvider(new FirebaseConfig(ApiKey));
                FirebaseAuthLink authConfiguration = await firebaseConfiguration
                    .SignInWithEmailAndPasswordAsync(AuthEmail, AuthPassword);

                FirebaseStorage storage = new FirebaseStorage(
                    Bucket,
                    new FirebaseStorageOptions {
                        AuthTokenAsyncFactory = () => Task.FromResult(authConfiguration.FirebaseToken),
                        ThrowOnCancel = true
                    });

                await storage
                    .Child("images")
                    .Child("ingredients")
                    .Child(exactFileName) // Use the fileName directly as the child reference
                    .DeleteAsync();

                firebaseConfiguration.Dispose();
            }
        }

        public static string GetImageNameFromUrl(string url) {
            int lastSeparatorIndex = url.LastIndexOf("%2F"); // Get the index of the last "%2F"
            int startIndex = lastSeparatorIndex + 3; // Start index of the desired string
            int endIndex = url.IndexOf("?alt", startIndex); // End index of the desired string
            string extractedString = url.Substring(startIndex, endIndex - startIndex); // Extract the string between the last "%2F" and before "?alt"
            return extractedString;
        }

    }
}

