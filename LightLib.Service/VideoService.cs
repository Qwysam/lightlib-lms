﻿using System.Collections.Generic;
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
    /// Handles Video (Library Asset) business logic 
    /// </summary>
    public class VideoService : IVideoService {
        
        private readonly LibraryDbContext _context;
        private readonly IMapper _mapper;
        private readonly Paginator<Video> _paginator;

        public VideoService(LibraryDbContext context, IMapper mapper) {
            _context = context;
            _mapper = mapper;
            _paginator= new Paginator<Video>();
        }

        /// <summary>
        /// Gets a paginated collection of Videos 
        /// </summary>
        /// <param name="page"></param>
        /// <param name="perPage"></param>
        /// <returns></returns>
        public async Task<PaginationResult<VideoDto>> GetAll(int page, int perPage) {
            var videos = _context.Videos;

            var pageOfVideos = await _paginator 
                .BuildPageResult(videos, page, perPage, b => b.Id)
                .ToListAsync();
            
            var paginatedVideos = _mapper.Map<List<VideoDto>>(pageOfVideos);
            
            return new PaginationResult<VideoDto> {
                Results = paginatedVideos,
                PerPage = perPage,
                PageNumber = page
            };
        }

        /// <summary>
        /// Gets a paginated list of Videos by a given Director
        /// </summary>
        /// <param name="director"></param>
        /// <param name="page"></param>
        /// <param name="perPage"></param>
        /// <returns></returns>
        public async Task<PaginationResult<VideoDto>> GetByDirector(
            string director, int page, int perPage) {
            var videos = _context.Videos.Where(v => v.Director.Contains(director));

            var pageOfVideos = await _paginator 
                .BuildPageResult(videos, page, perPage, b => b.Id)
                .ToListAsync();
            
            var paginatedVideos = _mapper.Map<List<VideoDto>>(pageOfVideos);
            
            return new PaginationResult<VideoDto> {
                Results = paginatedVideos,
                PerPage = perPage,
                PageNumber = page
            };
        }

        /// <summary>
        /// Gets a Video Library Asset by ID
        /// </summary>
        /// <param name="videoId"></param>
        /// <returns></returns>
        public async Task<VideoDto> Get(int videoId) {
            var video = await _context.Videos.FirstAsync(p => p.Id == videoId);
            return _mapper.Map<VideoDto>(video);
        }

        /// <summary>
        /// Creates a new Video Library Asset
        /// </summary>
        /// <param name="videoDto"></param>
        /// <returns></returns>
        public async Task<bool> Add(VideoDto videoDto) {
            var video = _mapper.Map<Video>(videoDto);
            await _context.AddAsync(video);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
