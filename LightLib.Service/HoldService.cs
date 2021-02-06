using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using LightLib.Data;
using LightLib.Data.Models;
using LightLib.Models;
using LightLib.Models.DTOs;
using LightLib.Service.Helpers;
using LightLib.Service.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace LightLib.Service {
    /// <summary>
    /// Handles business logic for Holds
    /// </summary>
    public class HoldService : IHoldService {
        
        private readonly LibraryDbContext _context;
        private readonly IMapper _mapper;
        private readonly Paginator<Hold> _holdsPaginator;

        public HoldService(
            LibraryDbContext context,
            IMapper mapper) {
            _context = context;
            _mapper = mapper;
            _holdsPaginator = new Paginator<Hold>();
        }
        
        /// <summary>
        /// Gets a paginated list of current Holds for a given Library Asset ID
        /// </summary>
        /// <param name="libraryAssetId"></param>
        /// <param name="page"></param>
        /// <param name="perPage"></param>
        /// <returns></returns>
        public async Task<PaginationResult<HoldDto>> GetCurrentHolds(
            int libraryAssetId, int page, int perPage) {
            
            var holds = _context.Holds
                .Include(h => h.LibraryAsset)
                .Where(a => a.LibraryAsset.Id == libraryAssetId);

            var pageOfHolds = await _holdsPaginator
                .BuildPageResult(holds, page, perPage, h => h.HoldPlaced)
                .ToListAsync();

            var paginatedHolds = _mapper.Map<List<HoldDto>>(pageOfHolds);
            
            return new PaginationResult<HoldDto> {
                Results = paginatedHolds,
                PerPage = perPage,
                PageNumber = page
            };
        }
        
        /// <summary>
        /// Gets the corresponding Patron for a Given Hold ID
        /// </summary>
        /// <param name="holdId"></param>
        /// <returns></returns>
        public async Task<string> GetCurrentHoldPatron(int holdId) {
            var hold = _context.Holds
                .Include(a => a.LibraryAsset)
                .Include(a => a.LibraryCard)
                .Where(v => v.Id == holdId);

            var cardId = await hold
                .Include(a => a.LibraryCard)
                .Select(a => a.LibraryCard.Id)
                .FirstAsync();

            var patron = await _context.Patrons
                .Include(p => p.LibraryCard)
                .FirstAsync(p => p.LibraryCard.Id == cardId);

            return $"{patron.FirstName} {patron.LastName}";
        }

        /// <summary>
        /// Gets the date the given Hold was placed 
        /// </summary>
        /// <param name="holdId"></param>
        /// <returns></returns>
        public async Task<string> GetCurrentHoldPlaced(int holdId) {
            var hold = await _context.Holds
                .Include(a => a.LibraryAsset)
                .Include(a => a.LibraryCard)
                .FirstAsync(v => v.Id == holdId);

            var holdPlaced = hold.HoldPlaced;

            return holdPlaced.ToString(CultureInfo.InvariantCulture);
        }
        
        /// <summary>
        /// Place a hold on a library asset for a given Library Asset ID and Library Card
        /// </summary>
        /// <param name="assetId"></param>
        /// <param name="libraryCardId"></param>
        public async Task<bool> PlaceHold(int assetId, int libraryCardId) {
            var now = DateTime.UtcNow;

            var asset = await _context.LibraryAssets
                .Include(a => a.Status)
                .FirstAsync(a => a.Id == assetId);

            var card = await _context.LibraryCards
                .FirstAsync(a => a.Id == libraryCardId);

            _context.Update(asset);

            if (asset.Status.Name == "Available") {
                asset.Status = await _context.Statuses
                    .FirstAsync(a => a.Name == "On Hold");
            }

            var hold = new Hold {
                HoldPlaced = now,
                LibraryAsset = asset,
                LibraryCard = card
            };

            await _context.AddAsync(hold);
            await _context.SaveChangesAsync();
            
            return true;
        }
        
        /// <summary>
        /// Returns the earliest hold, if any, for a given Library Asset ID
        /// </summary>
        /// <param name="libraryAssetId"></param>
        /// <returns></returns>
        public async Task<HoldDto> GetEarliestHold(int libraryAssetId) {
            var earliestHold = await _context.Holds
                .Include(hold => hold.LibraryAsset)
                .Include(hold => hold.LibraryCard)
                .Where(hold => hold.LibraryAsset.Id == libraryAssetId)
                .OrderBy(a => a.HoldPlaced)
                .FirstAsync();

            return _mapper.Map<HoldDto>(earliestHold);
        }
    }
}
