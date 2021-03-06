﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MusicHub.Classes;
using MusicHub.Data;
using MusicHub.Models.ArtistViewModels;
using MusicHub.Models.LocalModels;

namespace MusicHub.Controllers
{
    public class ArtistController : Controller
    {
        private readonly ApplicationDbContext _context;

        [TempData]
        public string LastDirection { get; set; } //Descending or Ascending

        public ArtistController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Artist
        [AllowAnonymous]
        public async Task<IActionResult> Index(string sortOrder, string searchString)
        {
            //set default value of sort direction.
            LastDirection = "Descending";
            //get sorted artist collection from db.
            var artists = await GetSortedArtists("name");

            return View(artists);
        }

        [HttpGet]
        public async Task<ActionResult> Sort(string sortBy, string searchString)
        {
            //getting sorted artists, if there is a search 
            //string - returns filtered list by that string.
            var artists = await GetSortedArtists(sortBy, searchString);
            //return the new collection to the partial view.
            return PartialView("_Partial_Artists_Table", artists);
        }

        [HttpGet]
        public async Task<ActionResult> UndoSearch()
        {
            //gets the full collection from db with a sorting operation.
            var artists = await GetSortedArtists("name", string.Empty, "Ascending");

            return PartialView("_Partial_Artists_Table", artists);
        }

        [HttpGet]
        public async Task<ActionResult> Search(string searchString)
        {
            //gets artists collection from db with a filtering operation.
            var artists = await GetSearchedArtists(searchString);

            return PartialView("_Partial_Artists_Table", artists);
        }

        private async Task<List<ArtistModel>> GetSearchedArtists(string searchString)
        {
            // Get all artists from DB
            var artists = await _context.Artists.ToListAsync();

            // Check if we got search string
            if (!string.IsNullOrEmpty(searchString))
            {
                var lower = searchString.ToLower();

                // search artists by name
                artists = artists.Where(s =>
                       s.Name.ToLower().Contains(lower)
                    || s.LastName.ToLower().Contains(lower)).ToList();
            }

            return artists;
        }

        private async Task<List<ArtistModel>> GetSortedArtists(string sortBy, string searchString = "", string direction = null)
        {
            //get artists collection from db by a filter word.
            var artists = await GetSearchedArtists(searchString);
            //switching the order of the sorting direction.
            if (string.IsNullOrEmpty(direction))
                LastDirection = LastDirection == "Ascending" ? "Descending" : "Ascending";
            else
                LastDirection = direction;

            switch (LastDirection)
            {
                case "Ascending":
                    switch (sortBy)
                    {
                        case "name":
                            artists = artists.OrderBy(a => a.Name).ToList();
                            break;
                        case "last_name":
                            artists = artists.OrderBy(a => a.LastName).ToList();
                            break;
                    }
                    break;
                case "Descending":
                    switch (sortBy)
                    {
                        case "name":
                            artists = artists.OrderByDescending(a => a.Name).ToList();
                            break;
                        case "last_name":
                            artists = artists.OrderByDescending(a => a.LastName).ToList();
                            break;
                    }
                    break;
            }

            return artists;
        }

        // GET: Artist/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var artistModel = await _context.Artists
                // Include the songs of this artist inside the model
                .Include(artist => artist.Songs)
                // We query this DB entity not for editing but for displaying
                // so we use AsNoTracking() for performance 
                // it won't be synced with the DB info on call
                .AsNoTracking()
                .SingleOrDefaultAsync(m => m.ID == id);
            if (artistModel == null)
            {
                return NotFound();
            }
            // Send the model to the Details view page
            return View(artistModel);
        }

        // GET: Artist/Create
        [Authorize(Roles = Consts.Admin)]
        public IActionResult Create()
        {
            // Populate all the songs because we use a new empty artist
            PopulateAssignedSongsData(new ArtistModel { Songs = new List<SongModel>() });
            // Now we can use the songs in the view
            return View();
        }

        // POST: Artist/Create
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [Authorize(Roles = Consts.Admin)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("ID,Name,LastName")] ArtistModel artistModel, string[] selectedSongs)
        {
            if (ModelState.IsValid)
            {
                _context.Add(artistModel);
                await _context.SaveChangesAsync();
                // Init empty song list befor updating
                artistModel.Songs = new List<SongModel>();
                // Update in the DB, assign the selected songs this artist
                UpdateArtistSongs(selectedSongs, artistModel);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(artistModel);
        }

        // GET: Artist/Edit/5 
        [Authorize(Roles = Consts.Admin)]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var artistModel = await _context.Artists
                // Include artist song inside the model
                .Include(artist => artist.Songs)
                .SingleOrDefaultAsync(m => m.ID == id);
            if (artistModel == null)
            {
                return NotFound();
            }
            PopulateAssignedSongsData(artistModel);
            // send the model to the view
            return View(artistModel);
        }

        // POST: Artist/Edit/5
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [Authorize(Roles = Consts.Admin)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int? id, string[] selectedSongs)
        {
            if (id == null)
            {
                return NotFound();
            }
            // Get the artist to update from the DB, included his songs
            var artistToUpdate = await _context.Artists
            .Include(artist => artist.Songs)
            .SingleOrDefaultAsync(m => m.ID == id);
            // Check if update operation is possible to current artist
            if (await TryUpdateModelAsync<ArtistModel>(
                artistToUpdate, "",
                i => i.Name, i => i.LastName, i => i.Songs))
            {
                UpdateArtistSongs(selectedSongs, artistToUpdate);
                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateException /* ex */)
                {
                    //Log the error (uncomment ex variable name and write a log.)
                    ModelState.AddModelError("", "Unable to save changes. " +
                        "Try again, and if the problem persists, " +
                        "see your system administrator.");
                }
                return RedirectToAction(nameof(Index));
            }

            UpdateArtistSongs(selectedSongs, artistToUpdate);
            PopulateAssignedSongsData(artistToUpdate);
            return View(artistToUpdate);
        }

        // GET: Artist/Delete/5
        [Authorize(Roles = Consts.Admin)]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var artistModel = await _context.Artists
                .SingleOrDefaultAsync(m => m.ID == id);
            if (artistModel == null)
            {
                return NotFound();
            }

            return View(artistModel);
        }

        // POST: Artist/Delete/5
        [HttpPost, ActionName("Delete")]
        [Authorize(Roles = Consts.Admin)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var artistModel = await _context.Artists.Include(artist => artist.Songs).SingleOrDefaultAsync(m => m.ID == id);
            // Update artist songs to unassign current artist
            UpdateArtistSongs(new String[0], artistModel);
            // Remove the artist from the DB
            _context.Artists.Remove(artistModel);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private void PopulateAssignedSongsData(ArtistModel artistModel)
        {
            var allSongs = _context.Songs.Include(song => song.Artist);
            var artistSongs = new HashSet<int>(artistModel.Songs.Select(song => song.ID));
            var viewModel = new List<AssignedSongData>();
            foreach (var song in allSongs)
            {
                viewModel.Add(new AssignedSongData
                {
                    SongID = song.ID,
                    Title = song.Name,
                    ArtistName = song?.Artist?.FullName,
                    Assigned = artistSongs.Contains(song.ID)
                });
            }
            //ViewBag.Songs = viewModel;
            ViewData["Songs"] = viewModel;
        }

        private void UpdateArtistSongs(string[] selectedSongs, ArtistModel artistToUpdate)
        {
            if (selectedSongs == null)
            {
                artistToUpdate.Songs = new List<SongModel>();
                return;
            }
            // For performance we use HashSet of the selected songs from the edit view of the Artist
            var selectedSongsHS = new HashSet<string>(selectedSongs);
            // The current songs of the artist
            var artistSongs = new HashSet<int>
                (artistToUpdate.Songs.Select(s => s.ID));
            // Iterate all songs in the DB
            foreach (var song in _context.Songs)
            {
                // Check if the current song from DB is selected by the view
                if (selectedSongsHS.Contains(song.ID.ToString()))
                {
                    // Assign the song to the artist, if the artist isn't assigned yet
                    if (!artistSongs.Contains(song.ID))
                    {
                        song.Artist = artistToUpdate;
                        song.ArtistId = artistToUpdate.ID;
                        _context.Update(song);
                    }
                }
                else
                {
                    // If we are here than the song is not selected, and if the artist currently assigned to this song
                    // we should than unassign the artist and update the song DB
                    if (artistSongs.Contains(song.ID))
                    {
                        song.Artist = null;
                        song.ArtistId = null;
                        _context.Update(song);
                    }
                }
            }
        }

        private bool ArtistModelExists(int id)
        {
            return _context.Artists.Any(e => e.ID == id);
        }
    }
}
