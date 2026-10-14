using AutoMapper;
using Camping.Core.DTOs.Requests;
using Camping.Core.DTOs.Responses;
using Camping.Core.Entities;
using Camping.Core.Querying;

namespace Camping.API.Mapping;

/// <summary>
/// AutoMapper configuration for the API contract.
///
/// For every entity there are two directions:
///   * Entity  -> Response  : straight projection of the public fields.
///   * Request -> Entity    : inbound data; server-managed and navigation
///                            members are explicitly ignored so the request
///                            DTO can never set them.
/// </summary>
public class MappingProfile : Profile
{
    public MappingProfile()
    {
        // ----- Paging wrapper (open generic): PagedResult<TSrc> -> PagedResult<TDest>.
        // The Items list is mapped element-wise using the entity->response maps below.
        CreateMap(typeof(PagedResult<>), typeof(PagedResult<>));

        // ----- User -----
        CreateMap<User, UserResponse>();
        CreateMap<UserRequest, User>()
            .ForMember(d => d.Id, o => o.Ignore())
            .ForMember(d => d.CreatedAt, o => o.Ignore())
            .ForMember(d => d.PasswordHash, o => o.Ignore())
            .ForMember(d => d.Reservations, o => o.Ignore());

        // ----- CampingSite -----
        CreateMap<CampingSite, CampingSiteResponse>();
        CreateMap<CampingSiteRequest, CampingSite>()
            .ForMember(d => d.Id, o => o.Ignore())
            .ForMember(d => d.CreatedAt, o => o.Ignore())
            .ForMember(d => d.Tents, o => o.Ignore())
            .ForMember(d => d.Amenities, o => o.Ignore());

        // ----- Tent -----
        CreateMap<Tent, TentResponse>();
        // Public occupancy: only the tent and the dates of a booking.
        CreateMap<Reservation, TentOccupancyResponse>();
        CreateMap<TentRequest, Tent>()
            .ForMember(d => d.Id, o => o.Ignore())
            .ForMember(d => d.RowVersion, o => o.Ignore())
            .ForMember(d => d.CampingSite, o => o.Ignore())
            .ForMember(d => d.Reservations, o => o.Ignore());

        // ----- Reservation -----
        // The display names come from the related rows loaded with Include/ThenInclude;
        // they stay null when those navigations were not loaded.
        CreateMap<Reservation, ReservationResponse>()
            .ForMember(d => d.TentLabel, o => o.MapFrom(s => s.Tent.Label))
            .ForMember(d => d.CampingSiteName, o => o.MapFrom(s => s.Tent.CampingSite.Name))
            .ForMember(d => d.GuestName, o => o.MapFrom(s => s.User.FullName));
        // UserId is never taken from the request - the controller sets it from the JWT.
        CreateMap<ReservationRequest, Reservation>()
            .ForMember(d => d.UserId, o => o.Ignore())
            .ForMember(d => d.Id, o => o.Ignore())
            .ForMember(d => d.CreatedAt, o => o.Ignore())
            .ForMember(d => d.TotalPrice, o => o.Ignore())
            .ForMember(d => d.Tent, o => o.Ignore())
            .ForMember(d => d.User, o => o.Ignore());

        // ----- Review -----
        // Response only: there is no Request -> Entity map because the author
        // comes from the JWT, never from the payload (see ReviewsController).
        CreateMap<Review, ReviewResponse>()
            .ForMember(d => d.AuthorName, o => o.MapFrom(s => s.User.FullName));

        // ----- Amenity -----
        CreateMap<Amenity, AmenityResponse>();
        CreateMap<AmenityRequest, Amenity>()
            .ForMember(d => d.Id, o => o.Ignore())
            .ForMember(d => d.CampingSites, o => o.Ignore());
    }
}
