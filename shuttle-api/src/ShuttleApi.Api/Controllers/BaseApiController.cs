using Microsoft.AspNetCore.Mvc;
using ShuttleApi.Application.Common.Mediator;

namespace ShuttleApi.Api.Controllers;

[ApiController]
public abstract class BaseApiController(ISender sender) : ControllerBase
{
    protected ISender Sender { get; } = sender;
}
