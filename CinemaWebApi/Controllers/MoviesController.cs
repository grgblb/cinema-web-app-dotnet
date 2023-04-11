using AutoMapper;
using CinemaWebApi.DTOs;
using CinemaWebApi.Entities;
using CinemaWebApi.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CinemaWebApi.Controllers;

[ApiController]
[Route("api/movies")]
public class MoviesController : Controller
{
    private readonly AppDbContext _context;
    private readonly IMapper _mapper;

    public MoviesController(AppDbContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    [HttpGet]
    public async Task<ActionResult<List<Movie>>> Get()
    {
        return await _context.Movies.ToListAsync();
    }

    [HttpGet("filter")]
    public async Task<ActionResult<List<Movie>>> Filter([FromQuery] FilterMoviesDto filterMoviesDto)
    {
        var moviesQueryable = _context.Movies.AsQueryable();

        if (!string.IsNullOrWhiteSpace(filterMoviesDto.Title))
        {
            moviesQueryable = moviesQueryable.Where(m => m.Title.Contains(filterMoviesDto.Title));
        }

        if (filterMoviesDto.UpcomingReleases)
        {
            moviesQueryable = moviesQueryable
                .Where(m => m.ReleaseDate.ToDateTime(TimeOnly.MinValue) > DateTime.Today)
                .OrderBy(m => m);
        }

        await HttpContext.InsertPaginationParamsInResponse(moviesQueryable, filterMoviesDto.RecordsPerPage);
       
        return await moviesQueryable.Paginate(filterMoviesDto).ToListAsync();
    }

    [HttpGet("{id:int}", Name="getMovie")]
    public async Task<ActionResult<Movie>> Get(int id)
    {
        var movie = await _context.Movies.FindAsync(id);

        if (movie is null)
        {
            return NotFound();
        }

        return movie;
    }

    [HttpPost]
    public async Task<ActionResult> Post([FromBody] MovieCreateDto movieCreateDto)
    {
        var movie = _mapper.Map<Movie>(movieCreateDto);
        AnnotateActorsOrder(movie);
        _context.Add(movie);
        await _context.SaveChangesAsync();
        return new CreatedAtRouteResult("getMovie", new { id = movie.Id }, movie);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult> Put(int id, [FromBody] MovieUpdateDto movieUpdateDto)
    {
        var movie = await _context.Movies.FindAsync(id);

        if (movie is null)
        {
            return NotFound();
        }

        _mapper.Map(movieUpdateDto, movie);

        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"delete from MovieActors where MovieId = {movie.Id}; delete from MovieGenres where MovieId = {movie.Id};"); 
        AnnotateActorsOrder(movie);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<ActionResult> Delete(int id)
    {
        var exists = await _context.Movies.FindAsync(id);
        if (exists is null)
        {
            return NotFound();
        }

        _context.Remove(exists);
        await _context.SaveChangesAsync();
        
        return NoContent();
    }

    private static void AnnotateActorsOrder(Movie movie)
    {
        for (var i = 0; i < movie.Actors.Count; i++)
        {
            movie.Actors[i].Order = i;
        }
    }
}