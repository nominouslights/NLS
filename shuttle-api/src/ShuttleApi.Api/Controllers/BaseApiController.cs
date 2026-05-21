using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ShuttleApi.Api.Controllers;

[ApiController]
public abstract class BaseApiController(ISender sender) : ControllerBase
{
    protected ISender Sender { get; } = sender;
}
