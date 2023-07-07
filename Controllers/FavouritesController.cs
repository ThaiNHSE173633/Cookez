﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using SmartBreadcrumbs.Attributes;
using Microsoft.AspNetCore.Authorization;
using Repositories;
using BusinessObjects.Models;

namespace CapstoneProject.Controllers {

    public class FavouritesController : Controller {

        private readonly IFavouriteRepository _favouriteRepository;
        private readonly IRecipeRepository _recipeRepository;
        private readonly UserManager<Account> _userManager;
        private readonly RecipeOrganizerContext _context;

        public FavouritesController(RecipeOrganizerContext context, IFavouriteRepository favouriteRepository, IRecipeRepository recipeRepository, UserManager<Account> userManager) {
            _favouriteRepository = favouriteRepository;
            _recipeRepository = recipeRepository;
            _userManager = userManager;
            _context = context;
        }

        //Can't handle JsonResult task with data call from Repository, temporary use RecipeOrganizerContext inject

        [HttpPost]
        public async Task<JsonResult> AddToFavourite(int favouriteId, int recipeId) {
            var favouriteList = _context.Favourites.Include(f => f.Recipes).FirstOrDefault(f => f.Id == favouriteId);
            var recipe = _context.Recipes.FirstOrDefault(f => f.Id == recipeId);
            favouriteList.Recipes.Add(recipe);
            await _context.SaveChangesAsync();
            return Json(true);
        }
        [HttpPost]
        public async Task<JsonResult> RemoveFromFavourite(int favouriteId, int recipeId) {
            var favouriteList = _context.Favourites.Include(f => f.Recipes).FirstOrDefault(f => f.Id == favouriteId);
            var recipe = _context.Recipes.FirstOrDefault(f => f.Id == recipeId);
            favouriteList.Recipes.Remove(recipe);
            await _context.SaveChangesAsync();
            return Json(true);
        }


        [HttpPost]
        [Authorize]
        public async Task<JsonResult> AddRecipe(int recipeId, int[] favouriteIds, int[] allfavouriteIds) {
            var entity = _context.Recipes.FirstOrDefault(item => item.Id == recipeId);
            if (entity != null) {
                foreach (var favouriteId in allfavouriteIds) {
                    var favourite = _context.Favourites.Include(a => a.Recipes).FirstOrDefault(a => a.Id == favouriteId);

                    if ((favouriteIds.Contains(favouriteId)) && (!favourite.Recipes.Contains(entity))) {
                        favourite.Recipes.Add(entity);
                        //TempData["success"] = "Add to favourites successfully";
                    } else
                    if (!(favouriteIds.Contains(favouriteId)) && (favourite.Recipes.Contains(entity))) {
                        favourite.Recipes.Remove(entity);
                        //TempData["success"] = "Removed from favourites successfully";
                    }
                }
                await _context.SaveChangesAsync();
            }

            return Json(new { success = true });
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Name,Description,isPrivate")] Favourite favourite) {
            if (ModelState.IsValid) {
                    favourite.Account = await _userManager.GetUserAsync(User);
                    _favouriteRepository.InsertFavourite(favourite);
                    return RedirectToAction(nameof(Index));
            }
            return View(favourite);
        }


        [HttpPost]
        public async Task<JsonResult> GetAllFavouriteRecipes(int[] recipeIds) {
            var favourites = new List<object>();

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser != null) {
                foreach (var recipeId in recipeIds) {
                    var favorite = _favouriteRepository.GetAllFavouriteRecipes(recipeId, currentUser);
                    if (favorite != null) {
                        favourites.Add(new { recipeId = recipeId, isFavorite = true });
                    } else {
                        favourites.Add(new { recipeId = recipeId, isFavorite = false });
                    }
                }
            }

            return Json(favourites);
        }
        //This is for favourite's checkboxes
        [HttpPost]
        public async Task<JsonResult> GetAllFavouriteLists(int recipeId, int[] favouriteIds) {
            var favourites = new List<object>();

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser != null) {
                foreach (var favouriteId in favouriteIds) {
                    var favorite = _favouriteRepository.GetAllFavouriteLists(recipeId, favouriteId, currentUser);
                    if (favorite != null) {
                        favourites.Add(new { id = favouriteId, isFavorite = true });
                    } else {
                        favourites.Add(new { id = favouriteId, isFavorite = false });
                    }
                }
            }

            return Json(favourites);
        }


        // GET: Favourites
        [Breadcrumb("My Collections")]
        [Authorize]
        public async Task<IActionResult> Index() {
            var currentUser = await _userManager.GetUserAsync(User);
            var userFavourite = _favouriteRepository.GetFavouritesIndex(currentUser);
            return View(userFavourite);
        }

        // GET: Favourites/Details/5
        [Breadcrumb("Collection Details")]
        public IActionResult Details(int? id) {

            var favouriteList = _favouriteRepository.GetFavouritesDetails(id);

            ViewData["Name"] = favouriteList?.Name;
            ViewData["Description"] = favouriteList?.Description;
            ViewData["IsPrivate"] = favouriteList?.isPrivate;
            ViewData["Id"] = id;
            if (favouriteList?.Account != null)
                ViewData["UserId"] = favouriteList.Account.Id;

            var recipes = favouriteList?.Recipes
                .ToList();

            return View(recipes);
        }

        // GET: Favourites/Create
        [Authorize]
        public IActionResult Create() {
            return View();
        }

        // GET: Favourites/Edit/5
        [Authorize]
        public IActionResult Edit(int? id) {
            if (id == null || _favouriteRepository.GetFavourites() == null) {
                return NotFound();
            }

            var favourite = _favouriteRepository.GetFavouriteById(id);
            if (favourite == null) {
                return NotFound();
            }
            return View(favourite);
        }


        [HttpPost]
        public async Task<JsonResult> Edit(int id, string name, string description, bool isPrivate) {
            var favourite = _context.Favourites.FirstOrDefault(f => f.Id == id);
            try {
                favourite.Name = name;
                favourite.Description = description;
                favourite.isPrivate = isPrivate;
                _context.Update(favourite);
                await _context.SaveChangesAsync();
            } catch (DbUpdateConcurrencyException) {
                if (!FavouriteExists(favourite.Id)) {
                    return null;
                } else {
                    throw;
                }
            }
            return Json(new { name = name, description = description, isPrivate = isPrivate });
        }

        // GET: Favourites/Delete/5
        [Authorize]
        public IActionResult Delete(int? id) {
            if (id == null || _favouriteRepository.GetFavourites() == null) {
                return NotFound();
            }

            var favourite = _favouriteRepository.GetFavouriteById(id);
            if (favourite == null) {
                return NotFound();
            }

            return View(favourite);
        }

        // POST: Favourites/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id) {
            if (_favouriteRepository.GetFavourites() == null) {
                return Problem("Entity set 'RecipeOrganizerContext.Favourites'  is null.");
            }
            var favourite = _favouriteRepository.GetFavouriteById(id);
            if (favourite != null) {
                _favouriteRepository.DeleteFavourite(favourite);
            }

            return RedirectToAction(nameof(Index));
        }

        private bool FavouriteExists(int id) {
            return (_favouriteRepository.GetFavourites()?.Any(e => e.Id == id)).GetValueOrDefault();
        }
    }
}