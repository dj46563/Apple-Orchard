<?php include "../inc/dbinfo.inc"; ?>
<?php 
    http_response_code(400);

    $con = mysqli_connect(DB_SERVER, DB_USERNAME, DB_PASSWORD, DB_DATABASE);

    //Check that connection worked
    if (mysqli_connect_errno()) {
        die("1\tConnect error: " . mysqli_connect_error());
    }

    $username = $_POST["username"];
    $password = $_POST["password"];

    // Get salt of this username
    $nameQuery = sprintf("SELECT id, salt, hash, apples FROM players WHERE username='%s'", $username);
    $nameCheck = mysqli_query($con, $nameQuery) or die ("2\tSalt check failed");

    if (mysqli_num_rows($nameCheck) == 0){
        echo "3\tUsername does not exist";
        exit();
    }

    $playerInfo = mysqli_fetch_assoc($nameCheck);

    $genHash = crypt($password, $playerInfo["salt"]);
    if ($genHash != $playerInfo["hash"]) {
        die("4\tIncorrect Password");
    }
    else {
        http_response_code(200);
        echo($playerInfo["hash"]);
    }
?>