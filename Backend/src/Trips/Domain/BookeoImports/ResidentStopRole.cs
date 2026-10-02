namespace NorthernLink.Trips.Domain.BookeoImports;

/// <summary>
/// Which end of the trip a "&lt;Community&gt; Residents" passenger category names. On a
/// "Shuttle to Thompson" product the community is where residents board
/// (<see cref="Pickup"/>); on "Shuttle from Thompson" it is where they get off
/// (<see cref="Dropoff"/>). The other end is always the route's endpoint for the trip's direction.
/// </summary>
public enum ResidentStopRole
{
    Pickup,
    Dropoff,
}
