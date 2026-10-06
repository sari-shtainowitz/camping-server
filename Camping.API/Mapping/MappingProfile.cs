using AutoMapper;
using Camping.API.DTOs.Requests;
using Camping.API.DTOs.Responses;
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
        CreateMap<TentRequest, Tent>()
            .ForMember(d => d.Id, o => o.Ignore())
            .ForMember(d => d.RowVersion, o => o.Ignore())
            .ForMember(d => d.CampingSite, o => o.Ignore())
            .ForMember(d => d.Reservations, o => o.Ignore());

        // ----- Reservation -----
        CreateMap<Reservation, ReservationResponse>();
        CreateMap<ReservationRequest, Reservation>()
            .ForMember(d => d.Id, o => o.Ignore())
            .ForMember(d => d.CreatedAt, o => o.Ignore())
            .ForMember(d => d.TotalPrice, o => o.Ignore())
            .ForMember(d => d.Tent, o => o.Ignore())
            .ForMember(d => d.User, o => o.Ignore());

        // ----- Amenity -----
        CreateMap<Amenity, AmenityResponse>();
        CreateMap<AmenityRequest, Amenity>()
            .ForMember(d => d.Id, o => o.Ignore())
            .ForMember(d => d.CampingSites, o => o.Ignore());
    }
}
