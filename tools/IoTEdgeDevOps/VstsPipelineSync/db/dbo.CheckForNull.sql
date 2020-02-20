CREATE   proc dbo.CheckForNull @i int 
as
begin
  if @i is null 
    raiserror('The value for @i should not be null', 15, 1) -- with log 

end
GO