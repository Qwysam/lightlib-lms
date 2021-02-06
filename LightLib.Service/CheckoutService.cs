﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using LightLib.Data;
using LightLib.Data.Models;
using LightLib.Models;
using LightLib.Models.DTOs;
using LightLib.Models.Exceptions;
using LightLib.Service.Helpers;
using LightLib.Service.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace LightLib.Service {
    /// <summary>
    /// Handles Library Asset Checkout / Checkin / Lost / Found business logic
    /// </summary>
    public class CheckoutService : ICheckoutService {
        private readonly LibraryDbContext _context;
        private readonly IMapper _mapper;
        private readonly Paginator<Hold> _holdsPaginator;
        private readonly Paginator<Checkout> _checkoutPaginator;
        private readonly Paginator<CheckoutHistory> _checkoutHistoryPaginator;
        private readonly IHoldService _holdService;

        public CheckoutService(
            LibraryDbContext context,
            IHoldService holdService,
            IMapper mapper) {
            _context = context;
            _holdService = holdService;
            _mapper = mapper;
            _holdsPaginator = new Paginator<Hold>();
            _checkoutPaginator = new Paginator<Checkout>();
            _checkoutHistoryPaginator = new Paginator<CheckoutHistory>();
        }

        /// <summary>
        /// Returns a paginated result of Checkouts
        /// </summary>
        /// <param name="page"></param>
        /// <param name="perPage"></param>
        /// <returns></returns>
        public async Task<PaginationResult<CheckoutDto>> GetAll(int page, int perPage) {
            var checkouts = _context.Checkouts;

            var pageOfCheckouts = await _checkoutPaginator 
                .BuildPageResult(checkouts, page, perPage, b => b.Since)
                .ToListAsync();
            
            var paginatedCheckouts = _mapper.Map<List<CheckoutDto>>(pageOfCheckouts);
            
            return new PaginationResult<CheckoutDto> {
                Results = paginatedCheckouts,
                PerPage = perPage,
                PageNumber = page
            };
        }
        
        /// <summary>
        /// Returns an paginated Checkout History ordered by latest checked-out date
        /// </summary>
        /// <param name="libraryAssetId"></param>
        /// <param name="page"></param>
        /// <param name="perPage"></param>
        /// <returns></returns>
        public async Task<PaginationResult<CheckoutHistoryDto>> GetCheckoutHistory(
            int libraryAssetId, 
            int page, 
            int perPage) {
            
            var checkoutHistories = _context.CheckoutHistories
                .Include(a => a.LibraryAsset)
                .Include(a => a.LibraryCard)
                .Where(a => a.LibraryAsset.Id == libraryAssetId);

            var pageOfHistory = await _checkoutHistoryPaginator
                .BuildPageResult(checkoutHistories, page, perPage, ch => ch.CheckedOut)
                .ToListAsync();

            var paginatedHistories = _mapper.Map<List<CheckoutHistoryDto>>(pageOfHistory);
            
            return new PaginationResult<CheckoutHistoryDto> {
                Results = paginatedHistories,
                PerPage = perPage,
                PageNumber = page
            };
        }

        /// <summary>
        /// Get the Checkout corresponding to the given ID
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<CheckoutDto> Get(int id) {
            var checkout = await _context.Checkouts
                .FirstAsync(p => p.Id == id);
            return _mapper.Map<CheckoutDto>(checkout);
        }

        /// <summary>
        /// Gets the latest Checkout for a given Library Asset ID
        /// </summary>
        /// <param name="libraryAssetId"></param>
        /// <returns></returns>
        public async Task<CheckoutDto> GetLatestCheckout(int libraryAssetId) {
            var latest = await _context.Checkouts
                .Where(c => c.LibraryAsset.Id == libraryAssetId)
                .OrderByDescending(c => c.Since)
                .FirstAsync();
            return _mapper.Map<CheckoutDto>(latest);
        }

        /// <summary>
        /// Returns true if a given Library Asset ID is checked out
        /// </summary>
        /// <param name="libraryAssetId"></param>
        /// <returns></returns>
        public async Task<bool> IsCheckedOut(int libraryAssetId) 
            => await _context.Checkouts .AnyAsync(a => a.LibraryAsset.Id == libraryAssetId);

        /// <summary>
        /// Get the patron who has the given Library Asset ID checked out
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<string> GetCurrentPatron(int id) {
            var checkout = await _context.Checkouts
                .Include(a => a.LibraryAsset)
                .Include(a => a.LibraryCard)
                .FirstAsync(a => a.LibraryAsset.Id == id);

            if (checkout == null) {
                // TODO
            }

            var cardId = checkout.LibraryCard.Id;

            var patron = await _context.Patrons
                .Include(p => p.LibraryCard)
                .FirstAsync(c => c.LibraryCard.Id == cardId);

            return $"{patron.FirstName} {patron.LastName}";
        }
        
        /// <summary>
        /// Add a checkout given a Checkout DTO representing a new instance
        /// </summary>
        /// <param name="newCheckoutDto"></param>
        /// <returns></returns>
        public async Task<bool> Add(CheckoutDto newCheckoutDto) {
            var checkoutEntity = _mapper.Map<Checkout>(newCheckoutDto);
            try {
                await _context.AddAsync(checkoutEntity);
                await _context.SaveChangesAsync();
                return true;
            } catch (Exception ex) {
                throw new LibraryServiceException(Reason.UncaughtError);
            }
        }

        /// <summary>
        /// Checks the provided Library Asset out to the provided Library Card
        /// </summary>
        /// <param name="assetId"></param>
        /// <param name="libraryCardId"></param>
        /// <returns></returns>
        public async Task<bool> CheckOutItem(int assetId, int libraryCardId) {

            var now = DateTime.UtcNow;

            var isAlreadyCheckedOut = await IsCheckedOut(assetId);
                
            if (isAlreadyCheckedOut) {
                // TODO
            }

            var libraryAsset = await _context.LibraryAssets
                .Include(a => a.Status)
                .FirstAsync(a => a.Id == assetId);

            _context.Update(libraryAsset);

            // TODO
            libraryAsset.Status = await _context.Statuses
                .FirstAsync(a => a.Name == "Checked Out");

            var libraryCard = await _context.LibraryCards
                .Include(c => c.Checkouts)
                .FirstAsync(a => a.Id == libraryCardId);

            var checkout = new Checkout {
                LibraryAsset = libraryAsset,
                LibraryCard = libraryCard,
                Since = now,
                Until = GetDefaultDateDue(now)
            };

            await _context.AddAsync(checkout);

            var checkoutHistory = new CheckoutHistory {
                CheckedOut = now,
                LibraryAsset = libraryAsset,
                LibraryCard = libraryCard
            };

            await _context.AddAsync(checkoutHistory);
            await _context.SaveChangesAsync();
            return true;
        }

        /// <summary>
        /// Checks in the given Library Asset ID
        /// </summary>
        /// <param name="assetId"></param>
        /// <returns></returns>
        public async Task<bool> CheckInItem(int assetId) {
            
            var now = DateTime.UtcNow;

            var libraryAsset = await _context.LibraryAssets
                .FirstAsync(a => a.Id == assetId);

            _context.Update(libraryAsset);

            // remove any existing checkouts on the item
            var checkout = await _context.Checkouts
                .Include(c => c.LibraryAsset)
                .Include(c => c.LibraryCard)
                .FirstAsync(a => a.LibraryAsset.Id == assetId);
            
            if (checkout != null) {
                _context.Remove(checkout);
            }

            // close any existing checkout history
            var history = await _context.CheckoutHistories
                .Include(h => h.LibraryAsset)
                .Include(h => h.LibraryCard)
                .FirstAsync(h =>
                    h.LibraryAsset.Id == assetId 
                    && h.CheckedIn == null);
            
            if (history != null) {
                _context.Update(history);
                history.CheckedIn = now;
            }

            // if there are current holds, check out the item to the earliest
            // TODO
            var wasCheckedOutToNewHold = await CheckoutToEarliestHold(assetId);

            if (wasCheckedOutToNewHold) {
                // TODO
            }

            // otherwise, set item status to available
            // TODO magic string
            libraryAsset.Status = await _context.Statuses
                .FirstAsync(a => a.Name == "Available");

            await _context.SaveChangesAsync();
            return true;
        }

        /// <summary>
        /// Checks the given Library Asset ID out to the next Hold
        /// </summary>
        /// <param name="assetId"></param>
        /// <returns></returns>
        private async Task<bool> CheckoutToEarliestHold(int assetId) {

            var earliestHold = await _holdService.GetEarliestHold(assetId);
            
            if (earliestHold == null) {
                return false;
            }

            var card = earliestHold.LibraryCard;
            
            _context.Remove(earliestHold);
            await _context.SaveChangesAsync();
            
            // TODO
            var checkOutResult = await CheckOutItem(assetId, card.Id);
            
            return checkOutResult;
        }

        /// <summary>
        /// Gets default date an asset is due
        /// </summary>
        /// <param name="now"></param>
        /// <returns></returns>
        /// TODO Magic Number
        private static DateTime GetDefaultDateDue(DateTime now) => now.AddDays(30);
    }
}
