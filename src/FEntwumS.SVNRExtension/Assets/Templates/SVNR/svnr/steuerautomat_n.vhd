--! \file svnr/steuerautomat_n.vhd
--! \brief Abge�nderte Version des Steuerautomaten - Mit cpu_en Interface   
--! 
--! Dem Steuerautomat wurde ein Input Port `cpu_en` hinzugef�gt, mit dem das Verlassen des POWER_PN Zustands verhindert werden kann. Somit wird verhindert, dass der SVNR trotz Empfang einer Clock zu Laufen beginnt.

library ieee; -- Einbinden der ieee-Library
use ieee.std_logic_1164.all;
use ieee.numeric_std.all;
--USE ieee.std_logic_arith.all; 

-- Beschreibung der Schnittstelle
entity steuerautomat_n is
  port (
    reset_init : in std_logic;
    clk_t      : in std_logic;
    opcode     : in std_logic_vector (7 downto 0);

    cpu_en     : in std_logic;  -- from bootloader/debugger
    -- o_state    : out std_logic_vector(5 downto 0);  -- to bootloader/debugger
    o_step_fin    : out std_logic;  -- to bootloader/debugger

    greater_zero   : in std_logic;
    equal_zero     : in std_logic;
    less_zero      : in std_logic;
    clear_register : out std_logic;

    strobe                  : out std_logic;
    wr                      : out std_logic;
    address_select          : out std_logic_vector (1 downto 0);
    speicher_data_in_select : out std_logic_vector (1 downto 0);

    akku_data_in_select : out std_logic_vector (1 downto 0);
    load_akku           : out std_logic;

    load_befehlsregister : out std_logic;

    load_programmzaehler           : out std_logic;
    programmzaehler_data_in_select : out std_logic_vector (1 downto 0);

    load_hilfsregister   : out std_logic;
    alu_operand_b_select : out std_logic;
    calculate            : out std_logic

  );
end steuerautomat_n;

architecture verhalten of steuerautomat_n is
  type STATE is (
    POWER_ON, FETCH_1, FETCH_2, FETCH_3, FETCH_4, DECODE,
    OP11_1, OP11_2, OP11_3, OP11_4, -- 10
    OP18_1, OP18_2, OP18_3,
    OP12_1, OP12_2, OP12_3, OP12_4, OP12_5, OP12_6, OP12_7, --20
    OP28_1, OP28_2, OP28_3, OP28_4,
    OP21_1, OP21_2, OP21_3, OP21_4, OP21_5, OP21_6, OP21_7, OP21_8, --32
    OP30_bis_39_1, OP30_bis_39_2, OP30_bis_39_3, OP30_bis_39_4, OP30_bis_39_5,
    OP3C_bis_3D_1, OP3C_bis_3D_2, OP3C_bis_3D_3, OP3C_bis_3D_4, -- 41
    OP48_58_59_5A_1, OP48_58_59_5A_2, OP48_58_59_5A_3,
    OP41_51_52_53_1, OP41_51_52_53_2, OP41_51_52_53_3, OP41_51_52_53_4); --48

  signal steuerautomat_zustand : STATE;

begin

  process (reset_init, clk_t, opcode) is

    variable var_zustands_vektor : STATE;
    variable var_opcode : integer range 0 to 255;
  begin

    if reset_init = '1' then

      var_zustands_vektor := POWER_ON;

    elsif (clk_t'event and clk_t = '1' and cpu_en = '1') then

      case var_zustands_vektor is
        when POWER_ON =>
          var_zustands_vektor := FETCH_1;

          -- ##############         FETCH           ####################                                  
        when FETCH_1 =>
          var_zustands_vektor := FETCH_2;

        when FETCH_2 =>
          var_zustands_vektor := FETCH_3;

        when FETCH_3 =>
          var_zustands_vektor := FETCH_4;

        when FETCH_4 =>
          var_zustands_vektor := DECODE;

          -- ##############          DECODE         #####################

        when DECODE =>
          var_opcode := to_integer (unsigned (opcode));

          case var_opcode is

            when 16 => --hex 10:  keine Operation
              var_zustands_vektor := FETCH_1;

            when 17 => --hex 11:  Akku = (Inhalt von Adresse A)
              var_zustands_vektor := OP11_1;

            when 24 => --hex 18:  Akku = Wert W
              var_zustands_vektor := OP18_1;

            when 18 => --hex 12:  Akku = (Inhalt von Adresse, die durch den Inhalt von Adresse A bestimmt wird) 
              var_zustands_vektor := OP12_1;

            when 40 => --hex 28:  (Inhalt von Adresse A) = Akku
              var_zustands_vektor := OP28_1;

            when 33 => --hex 21:  (Inhalt von Adresse, die durch den Inhalt von Adresse A bestimmt wird)  = Akku
              var_zustands_vektor := OP21_1;

              -- Alle Alu-Operatoren mit speicher_data_out als Operand B
              -- Die Alu wertet von sich aus den OP-Code aus und fuehrt den entsprechenden Operator aus.
              -- Bei den Operatoren mit ur einem Operanden (Akku), d.h. bei den Operatoren NOT, +1, und -1 
              -- wird auch so getan als sei speicher_data_out auch der Operand B
              -- Dies schadet nicht denn in diesen Faellen nutzt die Alu von sich aus den Operand B nicht.

            when 48 => --hex 30:   +  Akku = (Akku + Inhalt von Adresse A)
              var_zustands_vektor := OP30_bis_39_1;
            when 49 => --hex 31:   -  Akku = (Akku - Inhalt von Adresse A)
              var_zustands_vektor := OP30_bis_39_1;
            when 50 => --hex 32:   *  Akku = (Akku * Inhalt von Adresse A)
              var_zustands_vektor := OP30_bis_39_1;
            when 51 => --hex 33:   /  Akku = (Akku / Inhalt von Adresse A)
              var_zustands_vektor := OP30_bis_39_1;
            when 52 => --hex 34:   AND  Akku = (Akku AND Inhalt von Adresse A)
              var_zustands_vektor := OP30_bis_39_1;
            when 53 => --hex 35:   OR  Akku = (Akku OR Inhalt von Adresse A)
              var_zustands_vektor := OP30_bis_39_1;
            when 54 => --hex 36:   NOT  Akku = NOT Akku
              var_zustands_vektor := OP30_bis_39_1;
            when 55 => --hex 37:   XOR  Akku = (Akku XOR Inhalt von Adresse A)
              var_zustands_vektor := OP30_bis_39_1;
            when 56 => --hex 38:   +1  Akku = (Akku + 1)
              var_zustands_vektor := OP30_bis_39_1;
            when 57 => --hex 39:   -1  Akku = (Akku - 1)
              var_zustands_vektor := OP30_bis_39_1;
              -- Die beiden Shiftbefehle der Alu haben als Operand B den Operanden aus dem Befehlsregister
            when 60 => --hex 3c:   Akku = Akku << Operand uas Befehlsregister
              var_zustands_vektor := OP3C_bis_3D_1;
            when 61 => --hex 3d:   Akku = Akku >> Operand uas Befehlsregister
              var_zustands_vektor := OP3C_bis_3D_1;
              -- Sprungbefehle "Springe zu Adresse"
            when 72 => --hex 48:   Springe zu Adresse A
              var_zustands_vektor := OP48_58_59_5A_1;

            when 88 => --hex 58:   Springe zu Adresse A  wenn ALU-Flag = 0
              if (equal_zero = '1') then
                var_zustands_vektor := OP48_58_59_5A_1;
              else
                var_zustands_vektor := FETCH_1;
              end if;

            when 89 => --hex 59:   Springe zu Adresse A  wenn ALU-Flag <> 0
              if ((greater_zero = '1') or (less_zero = '1')) then
                var_zustands_vektor := OP48_58_59_5A_1;
              else
                var_zustands_vektor := FETCH_1;
              end if;

            when 90 => --hex 5a:   Springe zu Adresse A  wenn ALU-Flag <= 0
              if ((equal_zero = '1') or (less_zero = '1')) then
                var_zustands_vektor := OP48_58_59_5A_1;
              else
                var_zustands_vektor := FETCH_1;
              end if;

              -- Sprungbefehle "Springe zu Adresse, die durch den Inhalt von Adresse A bestimmt wird"
            when 65 => --hex 41:   Springe zu Adresse, die durch den Inhalt von Adresse A bestimmt wird
              var_zustands_vektor := OP41_51_52_53_1;

            when 81 => --hex 51:   Springe zu Adresse, die durch den Inhalt von Adresse A bestimmt wird  
              if (equal_zero = '1') then --                    wenn ALU-Flag = 0
                var_zustands_vektor := OP41_51_52_53_1;
              else
                var_zustands_vektor := FETCH_1;
              end if;

            when 82 => --hex 52:   Springe zu Adresse, die durch den Inhalt von Adresse A bestimmt wird 
              if ((greater_zero = '1') or (less_zero = '1')) then --                   wenn ALU-Flag <> 0
                var_zustands_vektor := OP41_51_52_53_1;
              else
                var_zustands_vektor := FETCH_1;
              end if;

            when 83 => --hex 53:   Springe zu Adresse, die durch den Inhalt von Adresse A bestimmt wird  
              if ((equal_zero = '1') or (less_zero = '1')) then --                     wenn ALU-Flag <= 0                      
                var_zustands_vektor := OP41_51_52_53_1;
              else
                var_zustands_vektor := FETCH_1;
              end if;

            when others =>
              var_zustands_vektor := POWER_ON;

          end case;

          -- #############################################################
          -- ##############  OP11  Akku = (Inhalt von Adresse A)    #########

        when OP11_1 =>
          var_zustands_vektor := OP11_2;

        when OP11_2 =>
          var_zustands_vektor := OP11_3;

        when OP11_3 =>
          var_zustands_vektor := OP11_4;

        when OP11_4 =>
          var_zustands_vektor := FETCH_1;
          -- #############################################################
          -- ##############    OP18      Akku = Wert W       ############

        when OP18_1 =>
          var_zustands_vektor := OP18_2;

        when OP18_2 =>
          var_zustands_vektor := OP18_3;

        when OP18_3 =>
          var_zustands_vektor := FETCH_1;

          -- #############################################################
          -- ############   OP12      Akku = (Inhalt von Adresse, die durch den Inhalt von Adresse A bestimmt wird) ########

        when OP12_1 =>
          var_zustands_vektor := OP12_2;

        when OP12_2 =>
          var_zustands_vektor := OP12_3;

        when OP12_3 =>
          var_zustands_vektor := OP12_4;

        when OP12_4 =>
          var_zustands_vektor := OP12_5;

        when OP12_5 =>
          var_zustands_vektor := OP12_6;

        when OP12_6 =>
          var_zustands_vektor := OP12_7;

        when OP12_7 =>
          var_zustands_vektor := FETCH_1;

          -- #############################################################

          -- ##############  OP28  (Inhalt von Adresse A) = Akku    #########

        when OP28_1 =>
          var_zustands_vektor := OP28_2;

        when OP28_2 =>
          var_zustands_vektor := OP28_3;

        when OP28_3 =>
          var_zustands_vektor := OP28_4;

        when OP28_4 =>
          var_zustands_vektor := FETCH_1;
          -- #############################################################
          -- ############   OP21      (Inhalt von Adresse, die durch den Inhalt von Adresse A bestimmt wird) = Akku  ########

        when OP21_1 =>
          var_zustands_vektor := OP21_2;

        when OP21_2 =>
          var_zustands_vektor := OP21_3;

        when OP21_3 =>
          var_zustands_vektor := OP21_4;

        when OP21_4 =>
          var_zustands_vektor := OP21_5;

        when OP21_5 =>
          var_zustands_vektor := OP21_6;

        when OP21_6 =>
          var_zustands_vektor := OP21_7;

        when OP21_7 =>
          var_zustands_vektor := OP21_8;

        when OP21_8 =>
          var_zustands_vektor := FETCH_1;

          -- #############################################################

          -- ##############  OP30_bis_39  Akku = Akku  XX   (Inhalt von Adresse A)    #########

        when OP30_bis_39_1 =>
          var_zustands_vektor := OP30_bis_39_2;

        when OP30_bis_39_2 =>
          var_zustands_vektor := OP30_bis_39_3;

        when OP30_bis_39_3 =>
          var_zustands_vektor := OP30_bis_39_4;

        when OP30_bis_39_4 =>
          var_zustands_vektor := OP30_bis_39_5;

        when OP30_bis_39_5 =>
          var_zustands_vektor := FETCH_1;
          -- #############################################################

          -- ##############  OP3C_bis_3D  Akku = Akku  SHIFT   (Operand aus Befehlsregister)    #########

        when OP3C_bis_3D_1 =>
          var_zustands_vektor := OP3C_bis_3D_2;

        when OP3C_bis_3D_2 =>
          var_zustands_vektor := OP3C_bis_3D_3;

        when OP3C_bis_3D_3 =>
          var_zustands_vektor := OP3C_bis_3D_4;

        when OP3C_bis_3D_4 =>
          var_zustands_vektor := FETCH_1;
          -- #############################################################
          -- ##############  OP48_58_59_5A  Springe zu Adresse    #########

        when OP48_58_59_5A_1 =>
          var_zustands_vektor := OP48_58_59_5A_2;

        when OP48_58_59_5A_2 =>
          var_zustands_vektor := OP48_58_59_5A_3;

        when OP48_58_59_5A_3 =>
          var_zustands_vektor := FETCH_1;
          -- #############################################################

          -- ##############  OP41_51_52_53  Springe zu Adresse, die durch den Inhalt von Adresse A bestimmt wird    #########

        when OP41_51_52_53_1 =>
          var_zustands_vektor := OP41_51_52_53_2;

        when OP41_51_52_53_2 =>
          var_zustands_vektor := OP41_51_52_53_3;

        when OP41_51_52_53_3 =>
          var_zustands_vektor := OP41_51_52_53_4;

        when OP41_51_52_53_4 =>
          var_zustands_vektor := FETCH_1;
          -- #############################################################
        when others =>
          var_zustands_vektor := POWER_ON;
      end case;

    end if;
    -- Alle Ausgabesignale auf default (0)vorbelegen,
    -- so dass in den Zustaenden nur noch die Aenderungen
    -- abgearbeitet werden muessen
    clear_register <= '0';

    strobe <= '0';
    wr <= '0';

    address_select <= "00"; -- 00: befehlsregister operand  -- 01: programmzaehler_data_out  
    -- 10: hilfsregister_data_out

    speicher_data_in_select <= "00"; -- 00: befehlsregister operand  -- 01: akku_data_out                   
    -- 10: alu_data_out             -- 11: hilfsregister_data_out

    akku_data_in_select <= "00"; -- 00: befehlsregister operand  -- 01: speicher_data_out
    -- 10: alu_data_out                               -- 11: hilfsregister_data_out

    programmzaehler_data_in_select <= "00"; -- 00: zaehler zaehlt hoch                         
    -- 01: zaehler wird mit befehlsregister operand geladen
    -- 10: zaehler wird mit speicher_data_out geladen

    load_akku <= '0';
    load_befehlsregister <= '0';
    load_programmzaehler <= '0';
    load_hilfsregister <= '0';

    alu_operand_b_select <= '0'; -- 0: operand_b ist speicher_data_out
    -- 1: operand_b ist befehlsregister operand
    calculate <= '0';

    o_step_fin <= '0';

    case var_zustands_vektor is
      when POWER_ON =>
        clear_register <= '1';

        -- ##############         FETCH           ####################                                                                                          
      when FETCH_1 =>
        strobe <= '0';
        address_select <= "01"; -- 01: programmzaehler_data_out
        o_step_fin <= '1';  -- signalisiere dem Debugger das Enden des voherigen steps
      when FETCH_2 =>
        strobe <= '1';
        address_select <= "01"; -- 01: programmzaehler_data_out
        programmzaehler_data_in_select <= "00"; -- 0: zaehler zaehlt hoch 
      when FETCH_3 =>
        strobe <= '1';
        address_select <= "01"; -- 01: programmzaehler_data_out
        programmzaehler_data_in_select <= "00"; -- 0: zaehler zaehlt hoch                              

        load_befehlsregister <= '1';
        load_programmzaehler <= '1';
      when FETCH_4 =>
        strobe <= '0';
        address_select <= "00";
        programmzaehler_data_in_select <= "00";

        load_befehlsregister <= '0';
        load_programmzaehler <= '0';
        -- #############################################################
        -- ##############         DECODE           ####################         
      when DECODE =>
        clear_register <= '0';
        -- #############################################################                                
        -- ##############  OP11  Akku = (Inhalt von Adresse A)    #########
      when OP11_1 =>
        strobe <= '0';
        address_select <= "00"; -- 00: befehlsregister operand 
        akku_data_in_select <= "01"; -- select speicher_data_out
        load_akku <= '0';
      when OP11_2 =>
        strobe <= '1';
        address_select <= "00"; -- 00: befehlsregister operand 
        akku_data_in_select <= "01"; -- select speicher_data_out
        load_akku <= '0';

      when OP11_3 =>
        strobe <= '1';
        address_select <= "00"; -- 00: befehlsregister operand                                 
        akku_data_in_select <= "01"; -- select speicher_data_out
        load_akku <= '1';
      when OP11_4 =>
        strobe <= '0';
        address_select <= "00"; -- 00: befehlsregister operand                                 
        akku_data_in_select <= "00"; -- select speicher_data_out
        load_akku <= '0';
        -- #############################################################                                

        -- ##############    OP18      Akku = Wert W       ############
      when OP18_1 =>
        akku_data_in_select <= "00"; -- 00: befehlsregister operand
        load_akku <= '0';
      when OP18_2 =>
        akku_data_in_select <= "00"; -- 00: befehlsregister operand
        load_akku <= '1';

      when OP18_3 =>
        akku_data_in_select <= "00"; -- 00: befehlsregister operand
        load_akku <= '0';
        -- #############################################################
        -- ############   OP12      Akku = (Inhalt von Adresse, die durch den Inhalt von Adresse A bestimmt wird) ########
        -- 1.) Einlesen der Adresse von der gelesen werden soll ins Hilfsregister
      when OP12_1 =>
        strobe <= '0';
        address_select <= "00"; -- select Befehlsregister Operand                                      
      when OP12_2 =>
        strobe <= '1';
        address_select <= "00"; -- select Befehlsregister Operand
      when OP12_3 =>
        strobe <= '1';
        address_select <= "00"; -- select Befehlsregister Operand
        load_hilfsregister <= '1';
      when OP12_4 =>
        strobe <= '0';
        address_select <= "10"; -- select Hilfsregister                                
        load_hilfsregister <= '0';

        -- Ab jetzt steht die Adresse von der gelesen werden soll im Hilfsregister

        -- 2.) Lesen von der Adresse die im Hiofsregster steht in den Akku
      when OP12_5 =>
        strobe <= '1';
        address_select <= "10"; -- select Hilfsregister        
        akku_data_in_select <= "01"; -- select speicher_data_out
        load_akku <= '0';
      when OP12_6 =>
        strobe <= '1';
        address_select <= "10"; -- select Hilfsregister        
        akku_data_in_select <= "01"; -- select speicher_data_out
        load_akku <= '1';
      when OP12_7 =>
        strobe <= '0';
        address_select <= "00";
        akku_data_in_select <= "00";
        load_akku <= '0';
        -- #############################################################
        -- ##############  OP28  (Inhalt von Adresse A) = Akku   #########
      when OP28_1 =>
        strobe <= '0';
        wr <= '1';
        address_select <= "00"; -- 00: befehlsregister operand 
        speicher_data_in_select <= "01"; -- 01: akku_data_out               
      when OP28_2 =>
        strobe <= '1';
        wr <= '1';
        address_select <= "00"; -- 00: befehlsregister operand 
        speicher_data_in_select <= "01"; -- 01: akku_data_out               
      when OP28_3 =>
        strobe <= '0';
        wr <= '1';
        address_select <= "00"; -- 00: befehlsregister operand 
        speicher_data_in_select <= "01"; -- 01: akku_data_out               
      when OP28_4 =>
        strobe <= '0';
        wr <= '0';
        address_select <= "00";
        speicher_data_in_select <= "00";

        -- #############################################################
        -- ############   OP21   (Inhalt von Adresse, die durch den Inhalt von Adresse A bestimmt wird) = Akku      ########
        -- 1.) Einlesen der Adresse auf die geschreiben werden soll ins Hilfsregister
      when OP21_1 =>
        strobe <= '0';
        address_select <= "00"; -- select Befehlsregister Operand                                      
      when OP21_2 =>
        strobe <= '1';
        address_select <= "00"; -- select Befehlsregister Operand
      when OP21_3 =>
        strobe <= '1';
        address_select <= "00"; -- select Befehlsregister Operand
        load_hilfsregister <= '1';
      when OP21_4 =>
        strobe <= '0';
        address_select <= "10"; -- select Hilfsregister                                
        load_hilfsregister <= '0';

        -- Ab jetzt steht die Adresse auf die geschrieben werden soll im Hilfsregister

        -- 2.) Schreiben auf die Adresse die im Hilfsregister steht
      when OP21_5 =>
        strobe <= '0';
        wr <= '1';
        address_select <= "10"; -- select Hilfsregister
        speicher_data_in_select <= "01"; -- 01: akku_data_out               
      when OP21_6 =>
        strobe <= '1';
        wr <= '1';
        address_select <= "10"; -- select Hilfsregister
        speicher_data_in_select <= "01"; -- 01: akku_data_out               

      when OP21_7 =>
        strobe <= '0';
        wr <= '1';
        address_select <= "10"; -- select Hilfsregister
        speicher_data_in_select <= "01"; -- 01: akku_data_out  
      when OP21_8 =>
        strobe <= '0';
        wr <= '0';
        address_select <= "00";
        speicher_data_in_select <= "00";
        -- #############################################################
        -- ##############  OP30_bis_39  Akku = Akku XXX (Inhalt von Adresse A)    #########
      when OP30_bis_39_1 =>
        strobe <= '0';
        address_select <= "00"; -- 00: befehlsregister operand 
        akku_data_in_select <= "10"; -- 10: alu_data_out
        load_akku <= '0';
        alu_operand_b_select <= '0';
        calculate <= '0';
      when OP30_bis_39_2 =>
        strobe <= '1';
        address_select <= "00"; -- 00: befehlsregister operand 
        akku_data_in_select <= "10"; -- 10: alu_data_out
        load_akku <= '0';
        alu_operand_b_select <= '0';
        calculate <= '0';

      when OP30_bis_39_3 =>
        strobe <= '1';
        address_select <= "00"; -- 00: befehlsregister operand                                 
        akku_data_in_select <= "10"; -- 10: alu_data_out
        load_akku <= '0';
        alu_operand_b_select <= '0';
        calculate <= '1';
      when OP30_bis_39_4 =>
        strobe <= '1';
        address_select <= "00"; -- 00: befehlsregister operand                                 
        akku_data_in_select <= "10"; -- 10: alu_data_out
        load_akku <= '1';
        alu_operand_b_select <= '0';
        calculate <= '1';
      when OP30_bis_39_5 =>
        strobe <= '0';
        address_select <= "00";
        akku_data_in_select <= "00";
        load_akku <= '0';
        alu_operand_b_select <= '0';
        calculate <= '0';
        -- #############################################################
        -- ##############  OP3C_bis_3D  Akku = Akku SHIFT (Operand aus dem Befehlsregister)    #########
      when OP3C_bis_3D_1 =>
        akku_data_in_select <= "10"; -- 10: alu_data_out
        load_akku <= '0';
        alu_operand_b_select <= '1';
        calculate <= '0';
      when OP3C_bis_3D_2 =>
        akku_data_in_select <= "10"; -- 10: alu_data_out
        load_akku <= '0';
        alu_operand_b_select <= '1';
        calculate <= '1';

      when OP3C_bis_3D_3 =>
        akku_data_in_select <= "10"; -- 10: alu_data_out
        load_akku <= '1';
        alu_operand_b_select <= '1';
        calculate <= '1';

      when OP3C_bis_3D_4 =>
        akku_data_in_select <= "00";
        load_akku <= '0';
        alu_operand_b_select <= '0';
        calculate <= '0';
        -- #############################################################
        -- ##############  OP48_58_59_5A  Springe zu Adresse    #########
      when OP48_58_59_5A_1 =>
        programmzaehler_data_in_select <= "01"; -- 1: zaehler wird mit befehlsregister operand geladen
        load_programmzaehler <= '0';
      when OP48_58_59_5A_2 =>
        programmzaehler_data_in_select <= "01"; -- 1: zaehler wird mit befehlsregister operand geladen
        load_programmzaehler <= '1';

      when OP48_58_59_5A_3 =>
        programmzaehler_data_in_select <= "00";
        load_programmzaehler <= '0';

        -- #############################################################
        -- ##############  OP41_51_52_53  Springe zu Adresse, die durch den Inhalt von Adresse A bestimmt wird    #########
      when OP41_51_52_53_1 =>
        strobe <= '0';
        address_select <= "00"; -- 00: befehlsregister operand         
        programmzaehler_data_in_select <= "10"; -- 10: zaehler wird mit speicher_data_out geladen
        load_programmzaehler <= '0';

      when OP41_51_52_53_2 =>
        strobe <= '1';
        address_select <= "00"; -- 00: befehlsregister operand         
        programmzaehler_data_in_select <= "10"; -- 10: zaehler wird mit speicher_data_out geladen
        load_programmzaehler <= '0';

      when OP41_51_52_53_3 =>
        strobe <= '1';
        address_select <= "00"; -- 00: befehlsregister operand         
        programmzaehler_data_in_select <= "10"; -- 10: zaehler wird mit speicher_data_out geladen
        load_programmzaehler <= '1';

      when OP41_51_52_53_4 =>
        strobe <= '0';
        address_select <= "00";
        programmzaehler_data_in_select <= "00";
        load_programmzaehler <= '0';

        -- #############################################################
      when others =>
        clear_register <= '1';

    end case;
    steuerautomat_zustand <= var_zustands_vektor;
  end process;

end verhalten;