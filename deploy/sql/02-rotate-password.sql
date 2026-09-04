/*
    Rotate the application login's password.

        sqlcmd -S .\SQLEXPRESS -E -C -i deploy\sql\02-rotate-password.sql -v NewPassword="<new>"

    Then put the same password into the connection string in

        C:\Swarnakshi\app\appsettings.Production.json

    and restart the service:

        Restart-Service Swarnakshi

    Edit that file directly -- it is the one place the server's settings live, and a deployment
    never overwrites it. Leave Jwt:Key alone while you are in there: changing it signs every user
    out, and changing a database password does not have to.
*/

:on error exit
SET NOCOUNT ON;
GO

:setvar AppLogin "SivayaanHMS"

IF '$(NewPassword)' = '' OR '$(NewPassword)' = '$' + '(NewPassword)'
    RAISERROR('Pass the new password with:  -v NewPassword="<new>"', 20, 1) WITH LOG;
GO

ALTER LOGIN [$(AppLogin)] WITH PASSWORD = N'$(NewPassword)';
PRINT 'Password rotated for $(AppLogin). Update appsettings.Production.json and restart the service.';
GO
