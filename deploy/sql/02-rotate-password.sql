/*
    Rotate the application login's password.

        sqlcmd -S .\SQLEXPRESS -E -C -i deploy\sql\02-rotate-password.sql -v NewPassword="<new>"

    Then regenerate the settings file with the same password and restart the service:

        deploy\scripts\New-ProductionSettings.ps1 -DbPassword "<new>" -KeepJwtKey
        Restart-Service Swarnakshi

    Keep the JWT key: changing it signs everyone out. Changing the database password does not.
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
