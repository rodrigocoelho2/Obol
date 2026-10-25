/*
    Post-deployment script of Obol.Database.
    Runs after every publish of the dacpac. There is no reference data to seed: accounts are created from the Website
    (the first one becomes the admin) and every account fills itself from Trading 212 (the API or a CSV export).
*/
PRINT N'Obol.Database deployed.';
