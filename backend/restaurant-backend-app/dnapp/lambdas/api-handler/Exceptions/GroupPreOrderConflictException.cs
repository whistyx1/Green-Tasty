using System;
namespace APIHandler.Exceptions;
public class GroupPreOrderConflictException(string message) : Exception(message);
