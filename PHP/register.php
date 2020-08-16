<?php include "../inc/dbinfo.inc"; ?>
<?php 
    http_response_code(400);


    $con = mysqli_connect(DB_SERVER, DB_USERNAME, DB_PASSWORD, DB_DATABASE);

    //Check that connection worked
    if (mysqli_connect_errno()) {
        die("1. Connect error: " . mysqli_connect_error());
    }

    $username = $_POST["username"];
    $password = $_POST["password"];

    // Check if name exists
    $nameCheckQuery = "SELECT username FROM game.players WHERE username='" . $username . "';";

    $nameCheck = mysqli_query($con, $nameCheckQuery) or die("2. Name uniqueness check failed: " . mysqli_error($con));

    if (mysqli_num_rows($nameCheck) > 0){
        echo "3. Username already exists";
        exit(3);
    }

    // Create salt and hash for password
    $salt = "\$5\$rounds=5000\$" . "greenhams" . $username . "\$";
    $hash = crypt($password, $salt);

    // Add user to table
    $insertUserQuery = sprintf("INSERT INTO players (username, hash, salt) VALUES ('%s', '%s', '%s');", $username, $hash, $salt);
    mysqli_query($con, $insertUserQuery) or die("4. Insert player query failed " . mysqli_error($con));

    http_response_code(200);
    echo("0");
?>